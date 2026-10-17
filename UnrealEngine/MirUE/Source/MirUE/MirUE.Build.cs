using UnrealBuildTool;

public class MirUE : ModuleRules
{
	public MirUE(ReadOnlyTargetRules Target) : base(Target)
	{
		PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;

		PublicDependencyModuleNames.AddRange(new string[]
		{
			"Core",
			"CoreUObject",
			"Engine",
			"InputCore",
			"EnhancedInput",
			"Paper2D",
			"Json",
			"JsonUtilities",
			"UMG"
		});

		PrivateDependencyModuleNames.AddRange(new string[]
		{
			"Slate",
			"SlateCore",
			"Sockets",
			"NetCore"
		});

		PublicIncludePaths.AddRange(new string[] { "MirUE" });
	}
}
