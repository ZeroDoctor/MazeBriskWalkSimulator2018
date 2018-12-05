// Rubberband navmesh movement. Client moves, sends move to server, server
// rejects it if necessary.
//
// There are a lot of things to consider:
// - it needs to work for click movement (agent.destination)
// - it needs to work for wasd movement (agent.velocity)
// - server needs to broadcast the move to other clients
// - other clients need to see the correct positions after joining the area
// - server needs to check/reject positions if needed
// - agent.warp (on server) needs to be either detected and forced to clients,
//   or server scripts need to call ForcePositionToClients or similar
// - players shouldn't move while DEAD, TRADING, etc.
//
// The great part about this solution is that the client can move freely, but
// the server can still intercept with:
//   * agent.Warp
//   * agent.ResetPath
// => all those calls are detected here and forced to the client.
//
// agent.destination should always be set on client, never on server. Otherwise
// race conditions will happen where client and server both try to force their
// destinations and it's not sure which happened first, etc.
//
// Note: no LookAtY needed because we move everything via .destination
using UnityEngine;
using UnityEngine.AI;
using Mirror;

[DisallowMultipleComponent]
[NetworkSettings(sendInterval=0.1f)]
public class NetworkNavMeshAgentRubberbanding : NetworkBehaviour
{
    public NavMeshAgent agent; // assign in Inspector (instead of GetComponent)
    public Player entity;

    // remember last serialized values for dirty bit
    private Vector3 lastServerPosition;
    //Quaternion lastServerRotation;
    private Vector3 lastSentDestination;
    private Vector3 lastSentPosition;
    //Quaternion lastSentRotation;
    private Vector3 lastReceivedDestination;
    private Quaternion lastReceivedRotation;
    private double lastSentTime; // double for long term precision
    private bool hadPath;

    // epsilon for float/vector3 comparison (needed because of imprecision
    // when sending over the network, etc.)
    const float epsilon = 0.000001f; // * for smoothness

    [Header("Send Interval")]
    [Range(0, 1)] public float sendInterval = 0.1f; // every ... seconds
    public override float GetNetworkSendInterval() { return sendInterval; }

    [Header("Sync Rotation (don't change at runtime!)")]
    public bool syncRotationX = true;
    public bool syncRotationY = true;
    public bool syncRotationZ = true;

    // rotation compression. not public so that other scripts can't modify
    // it at runtime. alternatively we could send 1 extra byte for the mode
    // each time so clients know how to decompress, but the whole point was
    // to save bandwidth in the first place.
    // -> can still be modified in the Inspector while the game is running,
    //    but would cause errors immediately and be pretty obvious.
    [Tooltip("Compresses 16 Byte Quaternion into None=12, Some=6, Much=3, Lots=2 Byte")]
    [SerializeField] Compression compressRotation = Compression.Much;
    public enum Compression { None, Some, Much, Lots }; // easily understandable and funny

    // server
    Vector3 lastPosition;
    Quaternion lastRotation;

    // client
    public class DataPoint
    {
        public float timeStamp;
        public Vector3 position;
        public Quaternion rotation;
        public float movementSpeed;
    }
    // interpolation start and goal
    [HideInInspector] public DataPoint start;
    [HideInInspector] public DataPoint goal;

    // local authority send time
    float lastClientSendTime;

    // keep track of last internal server position here, so that we know if it
    // was modified by another component
    [Header("Rubberbanding")]
    public float rubberbandDistance = 5;
    Vector3 lastInternalServerPosition;

    // previous position before we set the current one. in case someone needs it
    // for interpolation etc.
    [HideInInspector] public Vector3 previousPosition;

    // component cache
    CharacterController controller;

    // validate a move (the 'rubber' part)
    bool ValidateMove(Vector3 position)
    {
        // there is virtually no way to cheat navmesh movement, since it will
        // never calcluate a path to a point that is not on the navmesh.
        // -> we only need to check if alive
        // -> and need to be IDLE or MOVING
        //    -> not while CASTING. the FSM resets path, but we don't event want
        //       to start it here. otherwise wasd movement could move a tiny bit
        //       while CASTING if Cmd sets destination and Player.UpateCASTING
        //       only resets it next frame etc.
        //    -> not while STUNNED.
        // -> maybe a distance check in case we get too far off from latency
        return entity.health > 0 &&
               (entity.state == "IDLE" || entity.state == "MOVING");
    }

/*     [Command]
    void CmdMovedClick(Vector3 destination, float stoppingDistance)
    {
        // rubberband (check if valid move)
        if (ValidateMove(destination))
        {
            // apply the move on the server
            agent.stoppingDistance = stoppingDistance;
            agent.destination = destination;
            lastReceivedDestination = destination;

            // set dirty to trigger a OnSerialize next time, so that other clients
            // know about the new position too
            SetDirtyBit(1);
        }
        else
        {
            // otherwise keep current position and set dirty so that OnSerialize
            // is trigger. it will warp eventually when getting too far away.
            SetDirtyBit(1);
        }
    } */

    void Awake()
    {
        controller = GetComponent<CharacterController>();

        // just to be sure..
        if (localPlayerAuthority) Debug.LogWarning("NetworkTransformRubberbanding doesn't need local authority, please disable it in NetworkIdentity.");
    }

    public override void OnStartServer()
    {
        lastInternalServerPosition = transform.position;
    }

    [Command]
    void CmdMovedWASD(Vector3 position, Quaternion rotation)
    {
        // rubberband (check if valid move)
        if (ValidateMove(position))
        {
            // set position via .destination to get free interpolation
            /* agent.stoppingDistance = 0;
            agent.destination = position; */
            transform.position = position;
            transform.rotation = rotation;
            lastReceivedDestination = position;
            lastReceivedRotation = rotation;

            // set dirty to trigger a OnSerialize next time, so that other clients
            // know about the new position too
            SetDirtyBit(1);
        }
        else
        {
            // otherwise keep current position and set dirty so that OnSerialize
            // is trigger. it will warp eventually when getting too far away.
            SetDirtyBit(1);
        }
    }

    bool HasPath()
    {
        return agent.hasPath || agent.pathPending; // might still be computed
    }

    void Update()
    {
        // detect move mode
        bool hasPath = false;
        if(agent != null) {
            hasPath = HasPath();
        }

        // server should detect teleports / react if we got too far off
        // do this BEFORE isLocalPlayer actions so that agent.ResetPath can be
        // detected properly? otherwise localplayer wasdmovement cmd may
        // overwrite it
        if (isServer)
        {
            // neither click or wasd movement, but position changed further than 'speed'?
            // then we must have teleported, no other way to move this fast.
            if(entity.GetType() == typeof(Player)) {
                // just use OnSerialize via SetDirtyBit only sync when position
                // changed. set dirty bits 0 or 1
                SetDirtyBit((uint)(HasMovedOrRotated() ? 1 : 0));

                // find out if another component modifed transform.position since
                // last time. we check it before modiying the position again.
                // -> this is necessary because the server might reset the position
                //    after respawning, but the client actually has authority over
                //    the position. we still need to be able to force it somewhere.
                if (!isLocalPlayer)
                {
                    //Debug.Log(name + " warpcheckDistance=" + Vector3.Distance(lastInternalServerPosition, transform.position)  + " / " + rubberbandDistance);
                    // != check is not good enough because precision might be lost
                    // over the network, especially when compressing. so check the
                    // distance
                    if (Vector3.Distance(lastInternalServerPosition, transform.position) > rubberbandDistance)
                    {
                        // force position on clients
                        RpcForcePosition(transform.position);

                        // reset interpolation points on server too so we don't
                        // interpolate from pre-teleport position to new one
                        start = null;
                        goal = null;
                    }
                }

            } else {
                if (!hasPath && agent.velocity == Vector3.zero &&
                    Vector3.Distance(transform.position, lastServerPosition) > agent.speed)
                {
                    // set NetworkNavMeshAgent dirty so that onserialize is
                    // triggered and the client receives the position change
                    SetDirtyBit(1);
                    //Debug.Log(name + " teleported!");
                }

                // different destination than the one that we received from the
                // client? then set dirty so it gets synced to others
                // (epsilon comparison needed for float precision over the network)
                if (HasPath() && Vector3.Distance(agent.destination, lastReceivedDestination) > epsilon)
                {
                    // set dirty so onserialize notifies others
                    SetDirtyBit(1);
                    //Debug.Log(name + " destination changed");

                    // reset last received destination so we don't detect it again
                    // until it changes again
                    lastReceivedDestination = agent.destination;
                }

                // detect agent.Reset:
                // - had a path before but not anymore?
                // - and we never reached the planned destination even though path was canceled?
                // (epsilon comparison needed for float precision over the network)
                if (hadPath && !hasPath && Vector3.Distance(transform.position, lastReceivedDestination) > epsilon)
                {
                    // set dirty so onserialize notifies others
                    SetDirtyBit(1);
                    //Debug.Log(name + " path was reset");

                    // reset last received destination so we don't detect it again
                    // until it changes again
                    lastReceivedDestination = agent.destination;

                    // send target rpc to the local player so he doesn't ignore it
                    // -> better than a 'bool forceReset' flag for OnSerialize
                    //    because new Cmds might come in before OnSerialize was
                    //    called, which could lead to race conditions
                    // -> not in host mode (!isLocalPlayer)
                    if (!isLocalPlayer) TargetResetMovement(connectionToClient);
                }

                lastServerPosition = transform.position;
                hadPath = hasPath;
            }
            
        }

        // local player can move freely
        // (do nothing if in host mode though. don't want to overwrite the
        //  player's destination with Cmds here.)
        if(isClient) {
            if(entity.GetType() == typeof(Player)) {
                 // always send to server if we and arent the server
                // (like NetworkTransform localAuthority mode)
                // -> only if connectionToServer has been initialized yet too
                if (!isServer && isLocalPlayer && connectionToServer != null)
                {
                    // check only each 'sendinterval', otherwise we send at whatever
                    // the player's tick rate is, which is like DDOS
                    if (Time.time - lastClientSendTime >= GetNetworkSendInterval())
                    {
                        if (HasMovedOrRotated())
                        {
                            // send message to server
                            NetworkWriter writer = new NetworkWriter();
                            SerializeIntoWriter(writer, true, transform.position, transform.rotation, compressRotation, syncRotationX, syncRotationY, syncRotationZ);
                            CmdClientToServerSync(writer.ToArray());
                        }
                        lastClientSendTime = Time.time;
                    }
                }

                // apply interpolation on client for all players
                // except for local player if he has authority and handles it himself
                if (!isLocalPlayer)
                {
                    // received one yet? (initialized?)
                    if (goal != null)
                    {
                        // teleport or interpolate
                        if (NeedsTeleport())
                        {
                            SetPositionAndRotation(goal.position, goal.rotation);
                        }
                        else
                        {
                            SetPositionAndRotation(InterpolatePosition(start, goal, transform.position), InterpolateRotation(start, goal, transform.rotation));
                        }
                    }
                }
            } else {
                if (isLocalPlayer && !isServer)
                {
                    // click movement and destination changed since last sync?
                    // then send the latest DESTINATION
                    if (hasPath && agent.destination != lastSentDestination)
                    {
                        // don't send all the time to not DDOS the server
                        // note: if we check time BEFORE detecting move then moves are
                        //       detected very late and cause additional latency and
                        //       might cause move->idle->move effect if second move
                        //       doesn't come in fast enough, etc.
                        if (NetworkTime.time > lastSentTime + GetNetworkSendInterval())
                        {
                            //CmdMovedClick(agent.destination, agent.stoppingDistance);
                            lastSentDestination = agent.destination;
                            lastSentTime = NetworkTime.time;
                        }
                    }
                    // wasd movement and velocity changed since last sync?
                    // then send the latest POSITION (not velocity)
                    // why is velocity not zero after stopping?
                    else if (!hasPath && agent.velocity != Vector3.zero && transform.position != lastSentPosition)
                    {
                        // don't send all the time to not DDOS the server
                        // note: if we check time BEFORE detecting move then moves are
                        //       detected very late and cause additional latency and
                        //       might cause move->idle->move effect if second move
                        //       doesn't come in fast enough, etc.
                        if (NetworkTime.time > lastSentTime + GetNetworkSendInterval())
                        {
                            CmdMovedWASD(transform.position, transform.rotation);
                            lastSentPosition = transform.position;
                            lastSentTime = NetworkTime.time;
                        }
                    }
                }
            }  
        }
         
    }

    [TargetRpc]
    void TargetResetMovement(NetworkConnection conn)
    {
        if(entity.GetType() == typeof(Player)) {
            entity.m_speed = 0;
        } else { 
            // reset path and velocity
            agent.ResetMovement();
        }
        
    }

    // serialization is needed by OnSerialize and by manual sending from authority
    static bool SerializeIntoWriter(NetworkWriter writer, bool initialState, Vector3 position, Quaternion rotation, Compression compressRotation, bool syncRotationX, bool syncRotationY, bool syncRotationZ)
    {
        // serialize position
        writer.Write(position);

        // serialize rotation
        // writing quaternion = 16 byte
        // writing euler angles = 12 byte
        // -> quaternion->euler->quaternion always works.
        // -> gimbal lock only occurs when adding.
        Vector3 euler = rotation.eulerAngles;
        if (compressRotation == Compression.None)
        {
            // write 3 floats = 12 byte
            if (syncRotationX) writer.Write(euler.x);
            if (syncRotationY) writer.Write(euler.y);
            if (syncRotationZ) writer.Write(euler.z);
        }
        else if (compressRotation == Compression.Some)
        {
            // write 3 shorts = 6 byte. scaling [0,360] to [0,65535]
            if (syncRotationX) writer.Write(Utils.ScaleFloatToUShort(euler.x, 0, 360, ushort.MinValue, ushort.MaxValue));
            if (syncRotationY) writer.Write(Utils.ScaleFloatToUShort(euler.y, 0, 360, ushort.MinValue, ushort.MaxValue));
            if (syncRotationZ) writer.Write(Utils.ScaleFloatToUShort(euler.z, 0, 360, ushort.MinValue, ushort.MaxValue));
        }
        else if (compressRotation == Compression.Much)
        {
            // write 3 byte. scaling [0,360] to [0,255]
            if (syncRotationX) writer.Write(Utils.ScaleFloatToByte(euler.x, 0, 360, byte.MinValue, byte.MaxValue));
            if (syncRotationY) writer.Write(Utils.ScaleFloatToByte(euler.y, 0, 360, byte.MinValue, byte.MaxValue));
            if (syncRotationZ) writer.Write(Utils.ScaleFloatToByte(euler.z, 0, 360, byte.MinValue, byte.MaxValue));
        }
        else if (compressRotation == Compression.Lots)
        {
            // try to compress 3 floats into 2 byte (only if XYZ sync, otherwise 1 per byte again)
            if (syncRotationX && syncRotationY && syncRotationZ)
            {
                // write 2 byte, 5 bits for each float
                writer.Write(Utils.PackThreeFloatsIntoUShort(euler.x, euler.y, euler.z, 0, 360));
            }
            else
            {
                // write 3 byte. scaling [0,360] to [0,255]
                if (syncRotationX) writer.Write(Utils.ScaleFloatToByte(euler.x, 0, 360, byte.MinValue, byte.MaxValue));
                if (syncRotationY) writer.Write(Utils.ScaleFloatToByte(euler.y, 0, 360, byte.MinValue, byte.MaxValue));
                if (syncRotationZ) writer.Write(Utils.ScaleFloatToByte(euler.z, 0, 360, byte.MinValue, byte.MaxValue));
            }
        }

        return true;
    }

    // server-side serialization
    // used for the server to broadcast positions to other clients too
    public override bool OnSerialize(NetworkWriter writer, bool initialState)
    {
        
        // always send position so client knows if he's too far off and needs warp
        // we also need it for wasd movement anyway
       
        
        if(entity.GetType() == typeof(Player)) {
            return SerializeIntoWriter(writer, initialState, transform.position, transform.rotation, compressRotation, syncRotationX, syncRotationY, syncRotationZ);
        } else {
            // always send speed in case it's modified by something
            writer.Write(transform.position);
            writer.Write(agent.speed);

            // click movement? then also send destination and stopping distance
            // (no need to send everything all the time, saves bandwidth)
            bool hasPath = agent.hasPath || agent.pathPending;
            writer.Write(hasPath);
            if (hasPath)
            {
                // destination
                writer.Write(agent.destination);

                // always send stopping distance because monsters might stop early etc.
                writer.Write(agent.stoppingDistance);
            }
        }
        

        return true;
    }

    // try to estimate movement speed for a data point based on how far it
    // moved since the previous one
    // => if this is the first time ever then we use our best guess:
    //    -> delta based on transform.position
    //    -> elapsed based on send interval hoping that it roughly matches
    static float EstimateMovementSpeed(DataPoint from, DataPoint to, Transform transform, float sendInterval)
    {
        Vector3 delta = to.position - (from != null ? from.position : transform.position);
        float elapsed = from != null ? to.timeStamp - from.timeStamp : sendInterval;
        return elapsed > 0 ? delta.magnitude / elapsed : 0; // avoid NaN
    }

    // serialization is needed by OnSerialize and by manual sending from authority
    public void DeserializeFromReader(NetworkReader reader, bool initialState)
    {
        // put it into a data point immediately
        DataPoint temp = new DataPoint();

        // deserialize position
        temp.position = reader.ReadVector3();

        // deserialize rotation
        if (compressRotation == Compression.None)
        {
            // read 3 floats = 16 byte
            float x = syncRotationX ? reader.ReadSingle() : transform.rotation.eulerAngles.x;
            float y = syncRotationY ? reader.ReadSingle() : transform.rotation.eulerAngles.y;
            float z = syncRotationZ ? reader.ReadSingle() : transform.rotation.eulerAngles.z;
            temp.rotation = Quaternion.Euler(x, y, z);
        }
        else if (compressRotation == Compression.Some)
        {
            // read 3 shorts = 6 byte. scaling [-32768,32767] to [0,360]
            float x = syncRotationX ? Utils.ScaleUShortToFloat(reader.ReadUInt16(), ushort.MinValue, ushort.MaxValue, 0, 360) : transform.rotation.eulerAngles.x;
            float y = syncRotationY ? Utils.ScaleUShortToFloat(reader.ReadUInt16(), ushort.MinValue, ushort.MaxValue, 0, 360) : transform.rotation.eulerAngles.y;
            float z = syncRotationZ ? Utils.ScaleUShortToFloat(reader.ReadUInt16(), ushort.MinValue, ushort.MaxValue, 0, 360) : transform.rotation.eulerAngles.z;
            temp.rotation = Quaternion.Euler(x, y, z);
        }
        else if (compressRotation == Compression.Much)
        {
            // read 3 byte. scaling [0,255] to [0,360]
            float x = syncRotationX ? Utils.ScaleByteToFloat(reader.ReadByte(), byte.MinValue, byte.MaxValue, 0, 360) : transform.rotation.eulerAngles.x;
            float y = syncRotationY ? Utils.ScaleByteToFloat(reader.ReadByte(), byte.MinValue, byte.MaxValue, 0, 360) : transform.rotation.eulerAngles.y;
            float z = syncRotationZ ? Utils.ScaleByteToFloat(reader.ReadByte(), byte.MinValue, byte.MaxValue, 0, 360) : transform.rotation.eulerAngles.z;
            temp.rotation = Quaternion.Euler(x, y, z);
        }
        else if (compressRotation == Compression.Lots)
        {
            // try to decompress 2 bytes into 3 floats (only if XYZ sync, otherwise 1 per byte again)
            if (syncRotationX && syncRotationY && syncRotationZ)
            {
                // read 2 byte, 5 bits per float
                float[] xyz = Utils.UnpackUShortIntoThreeFloats(reader.ReadUInt16(), 0, 360);
                temp.rotation = Quaternion.Euler(xyz[0], xyz[1], xyz[2]);
            }
            else
            {
                // read 3 byte. scaling [0,255] to [0,360]
                float x = syncRotationX ? Utils.ScaleByteToFloat(reader.ReadByte(), byte.MinValue, byte.MaxValue, 0, 360) : transform.rotation.eulerAngles.x;
                float y = syncRotationY ? Utils.ScaleByteToFloat(reader.ReadByte(), byte.MinValue, byte.MaxValue, 0, 360) : transform.rotation.eulerAngles.y;
                float z = syncRotationZ ? Utils.ScaleByteToFloat(reader.ReadByte(), byte.MinValue, byte.MaxValue, 0, 360) : transform.rotation.eulerAngles.z;
                temp.rotation = Quaternion.Euler(x, y, z);
            }
        }

        // timestamp
        temp.timeStamp = Time.time;

        // movement speed: based on how far it moved since last time
        // has to be calculated before 'start' is overwritten
        temp.movementSpeed = EstimateMovementSpeed(goal, temp, transform, GetNetworkSendInterval());

        // only assign start/goal if not local player
        if (!isLocalPlayer)
        {
            // reassign start wisely
            // -> first ever data point? then make something up for previous one
            //    so that we can start interpolation without waiting for next.
            if (start == null)
            {
                start = new DataPoint{
                    timeStamp=Time.time - GetNetworkSendInterval(),
                    position=transform.position,
                    rotation=transform.rotation,
                    movementSpeed=temp.movementSpeed
                };
            }
            // -> second or nth data point? then update previous, but:
            //    we start at where ever we are right now, so that it's
            //    perfectly smooth and we don't jump anywhere
            //
            //    example if we are at 'x':
            //
            //        A--x->B
            //
            //    and then receive a new point C:
            //
            //        A--x--B
            //              |
            //              |
            //              C
            //
            //    then we don't want to just jump to B and start interpolation:
            //
            //              x
            //              |
            //              |
            //              C
            //
            //    we stay at 'x' and interpolate from there to C:
            //
            //           x..B
            //            \ .
            //             \.
            //              C
            //
            else
            {
                float oldDistance = Vector3.Distance(start.position, goal.position);
                float newDistance = Vector3.Distance(goal.position, temp.position);

                start = goal;

                // teleport / lag / obstacle detection: only continue at current
                // position if we aren't too far away
                if (Vector3.Distance(transform.position, start.position) < oldDistance + newDistance)
                {
                    start.position = transform.position;
                    start.rotation = transform.rotation;
                }
            }

            // set new destination in any case. new data is best data.
            goal = temp;
        }
    }

    // client-side deserialization
    public override void OnDeserialize(NetworkReader reader, bool initialState)
    {
        if(entity.GetType() == typeof(Player)) {
            // deserialize in any case to make sure that we read the right amount of
            // data to avoid HLAPI readindex out of range bugs
            DeserializeFromReader(reader, initialState);

        } else {
            // read position, speed, movement type in any case, so that we read
            // exactly what we write
            Vector3 position = reader.ReadVector3();
            agent.speed = reader.ReadSingle();
            bool hasPath = reader.ReadBoolean();
            // click or wasd movement?
            if (hasPath)
            {
                // read destination and stopping distance
                Vector3 destination = reader.ReadVector3();
                float stoppingDistance = reader.ReadSingle();

                // ignore for local player since he can move freely
                if (!isLocalPlayer)
                {
                    // try setting destination if on navmesh
                    // (might not be while falling from the sky after joining etc.)
                    if (agent.isOnNavMesh)
                    {
                        agent.stoppingDistance = stoppingDistance;
                        agent.destination = destination;
                    }
                    else Debug.LogWarning("NetworkNavMeshAgent.OnSerialize: agent not on NavMesh, name=" + name + " position=" + transform.position + " destination=" + destination);
                }
            }
            else
            {
                // ignore for local player since he can move freely
                if (!isLocalPlayer)
                {
                    // set position via .destination to get free interpolation
                    agent.stoppingDistance = 0;
                    agent.destination = position;
                }
            }

            // rubberbanding: if we are too far off because of a rapid position
            // change or latency or server side teleport, then warp
            // -> agent moves 'speed' meter per seconds
            // -> if we are speed * 2 units behind, then we teleport
            //    (using speed is better than using a hardcoded value)
            // -> we use speed * 2 for update/network latency tolerance. player
            //    might have moved quit a bit already before OnSerialize was called
            //    on the server.
            if (Vector3.Distance(transform.position, position) > agent.speed * 2 && agent.isOnNavMesh)
            {
                agent.Warp(position);
                //Debug.Log(name + " rubberbanding to " + position);
            }
        }
    }

     // local player client sends sync message to server for broadcasting
    [Command]
    void CmdClientToServerSync(byte[] data)
    {
        if (data == null) return;

        // deserialize message
        NetworkReader reader = new NetworkReader(data);
        DeserializeFromReader(reader, true);

        // server-only mode does no interpolation to save computations,
        // but let's set the position directly
        if (isServer && !isClient)
            SetPositionAndRotation(goal.position, goal.rotation);

        // set dirty so that OnSerialize broadcasts it
        SetDirtyBit(1);
    }

    // where are we in the timeline between start and goal? [0,1]
    static float CurrentInterpolationFactor(DataPoint start, DataPoint goal)
    {
        if (start != null)
        {
            float difference = goal.timeStamp - start.timeStamp;

            // the moment we get 'goal', 'start' is supposed to
            // start, so elapsed time is based on:
            float elapsed = Time.time - goal.timeStamp;
            return difference > 0 ? elapsed / difference : 0; // avoid NaN
        }
        return 0;
    }

    static Vector3 InterpolatePosition(DataPoint start, DataPoint goal, Vector3 currentPosition)
    {
        if (start != null)
        {
            // Option 1: simply interpolate based on time. but stutter
            // will happen, it's not that smooth. especially noticeable if
            // the camera automatically follows the player
            //   float t = CurrentInterpolationFactor();
            //   return Vector3.Lerp(start.position, goal.position, t);

            // Option 2: always += speed
            // -> speed is 0 if we just started after idle, so always use max
            //    for best results
            float speed = Mathf.Max(start.movementSpeed, goal.movementSpeed);
            return Vector3.MoveTowards(currentPosition, goal.position, speed * Time.deltaTime);
        }
        return currentPosition;
    }

    static Quaternion InterpolateRotation(DataPoint start, DataPoint goal, Quaternion defaultRotation)
    {
        if (start != null)
        {
            float t = CurrentInterpolationFactor(start, goal);
            return Quaternion.Slerp(start.rotation, goal.rotation, t);
        }
        return defaultRotation;
    }

    // teleport / lag / stuck detection
    // -> checking distance is not enough since there could be just a tiny
    //    fence between us and the goal
    // -> checking time always works, this way we just teleport if we still
    //    didn't reach the goal after too much time has elapsed
    bool NeedsTeleport()
    {
        // calculate time between the two data points
        float startTime = start != null ? start.timeStamp : Time.time - GetNetworkSendInterval();
        float goalTime = goal != null ? goal.timeStamp : Time.time;
        float difference = goalTime - startTime;
        float timeSinceGoalReceived = Time.time - goalTime;
        return timeSinceGoalReceived > difference * 5;
    }

    // moved since last time we checked it?
    bool HasMovedOrRotated()
    {
        // moved or rotated?
        bool moved = lastPosition != transform.position;
        bool rotated = lastRotation != transform.rotation;

        // save last for next frame to compare
        lastPosition = transform.position;
        lastRotation = transform.rotation;

        return moved || rotated;
    }

    // this is the only place where we modify transform.position and rotation.
    // (useful in case we need hooks / special events / etc.)
    void SetPositionAndRotation(Vector3 position, Quaternion rotation)
    {
        // keep track of previous position
        previousPosition = transform.position;

        // change position
        transform.position = position;
        transform.rotation = rotation;

        // using a character controller? then 'fake move' it once so that the
        // controller updates isGrounded status etc. internally. useful to avoid
        // having to sync 'grounded' state over the network (for animations etc)
        if (controller) controller.SimpleMove(Vector3.zero);

        // remember the last one we set on the server
        // (not localplayer because he has authority)
        if (isServer && !isLocalPlayer)
            lastInternalServerPosition = transform.position;
    }

    // force set a position from the server to the client
    [ClientRpc]
    void RpcForcePosition(Vector3 position)
    {
        // reuse function for consistency and to make sure that all
        // 'set position' code is in one place
        SetPositionAndRotation(position, transform.rotation);
        Debug.LogWarning("server force position: " + name + ", " + position);

        // clear last interpolation points so we don't interpolate from the old
        // pre-teleport position to the new one
        start = null;
        goal = null;
    }

     // guess velocity based on last move. might be useful on server or for
    // other clients in case we need animations based on speed
    // (aka velocity.magnitude)
    // -> it's free information without extra bandwidth anyway
    // (otherwise it would be 12 bytes for each client->server cmd and 12 bytes
    //  for each observer for the syncvar, so a LOT)
    Vector3 lastVelocity;
    public Vector3 EstimateVelocity()
    {
        // wait until start and goal were initialized
        if (start != null && goal != null)
        {
            Vector3 delta = transform.position - previousPosition;
            float speed = Mathf.Max(start.movementSpeed, goal.movementSpeed);
            Vector3 velocity = delta.normalized * speed;

            // smooth it
            Vector3 averageVelocity = (velocity + lastVelocity) / 2;
            lastVelocity = velocity;
            return averageVelocity;
        }
        return Vector3.zero;
    }

    static void DrawDataPointGizmo(DataPoint data, Color color)
    {
        // use a little offset because transform.position might be in
        // the ground in many cases
        Vector3 offset = Vector3.up * 0.01f;

        // draw position
        Gizmos.color = color;
        Gizmos.DrawSphere(data.position + offset, 0.5f);

        // draw forward and up
        Gizmos.color = Color.blue; // like unity move tool
        Gizmos.DrawRay(data.position + offset, data.rotation * Vector3.forward);

        Gizmos.color = Color.green; // like unity move tool
        Gizmos.DrawRay(data.position + offset, data.rotation * Vector3.up);
    }

    // draw the data points for easier debugging
    void OnDrawGizmos()
    {
        if (start != null) DrawDataPointGizmo(start, Color.gray);
        if (goal != null) DrawDataPointGizmo(goal, Color.white);
    }

}
