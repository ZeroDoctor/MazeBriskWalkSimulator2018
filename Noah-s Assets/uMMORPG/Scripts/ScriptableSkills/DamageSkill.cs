// Base type for damage skill templates.
// => there may be target damage, targetless damage, aoe damage, etc.
using System.Text;
using UnityEngine;

public abstract class DamageSkill : ScriptableSkill
{
    public LevelBasedInt damage = new LevelBasedInt{baseValue=1};

    // tooltip
    public override string ToolTip(int skillLevel, bool showRequirements = false)
    {
        StringBuilder tip = new StringBuilder(base.ToolTip(skillLevel, showRequirements));
        tip.Replace("{DAMAGE}", damage.Get(skillLevel).ToString());
        return tip.ToString();
    }
}
