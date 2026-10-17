using UnrealBuildTool;
using System.Collections.Generic;

public class MirUEEditorTarget : TargetRules
{
	public MirUEEditorTarget(TargetInfo Target) : base(Target)
	{
		Type = TargetType.Editor;
		DefaultBuildSettings = BuildSettingsVersion.V4;
		IncludeOrderVersion = EngineIncludeOrderVersion.Unreal5_3;
		ExtraModuleNames.Add("MirUE");
	}
}
