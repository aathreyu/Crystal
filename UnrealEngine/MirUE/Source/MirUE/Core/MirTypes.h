// Mir core enums, mirrored from the Crystal engine's Shared/Enums.cs so that
// gameplay data (stats, actions, spells) maps 1:1 onto the original game.

#pragma once

#include "CoreMinimal.h"
#include "MirTypes.generated.h"

UENUM(BlueprintType)
enum class EMirClass : uint8
{
	Warrior = 0,
	Wizard = 1,
	Taoist = 2,
	Assassin = 3,
	Archer = 4
};

UENUM(BlueprintType)
enum class EMirGender : uint8
{
	Male = 0,
	Female = 1
};

/** 8-way isometric facing. Order matches MirDirection in the source engine. */
UENUM(BlueprintType)
enum class EMirDirection : uint8
{
	Up = 0,
	UpRight = 1,
	Right = 2,
	DownRight = 3,
	Down = 4,
	DownLeft = 5,
	Left = 6,
	UpLeft = 7
};

UENUM(BlueprintType)
enum class EMirObjectType : uint8
{
	None = 0,
	Player = 1,
	Item = 2,
	Merchant = 3,
	Spell = 4,
	Monster = 5,
	Deco = 6,
	Creature = 7,
	Hero = 8
};

UENUM(BlueprintType)
enum class EMirChatType : uint8
{
	Normal = 0,
	Shout = 1,
	System = 2,
	Hint = 3,
	Announcement = 4,
	Group = 5,
	WhisperIn = 6,
	WhisperOut = 7,
	Guild = 8,
	Trainer = 9,
	LevelUp = 10,
	System2 = 11,
	Relationship = 12
};

UENUM(BlueprintType)
enum class EMirAttackMode : uint8
{
	Peace = 0,
	Group = 1,
	Guild = 2,
	EnemyGuild = 3,
	RedBrown = 4,
	All = 5
};

UENUM(BlueprintType)
enum class EMirEquipmentSlot : uint8
{
	Weapon = 0,
	Armour = 1,
	Helmet = 2,
	Torch = 3,
	Necklace = 4,
	BraceletL = 5,
	BraceletR = 6,
	RingL = 7,
	RingR = 8,
	Amulet = 9,
	Belt = 10,
	Boots = 11,
	Stone = 12,
	Mount = 13
};

/** MirAction animation states, indexed identically to the .Lib FrameSet. */
UENUM(BlueprintType)
enum class EMirAction : uint8
{
	Standing = 0,
	Walking = 1,
	Running = 2,
	Pushed = 3,
	Stance = 7,
	Attack1 = 9,
	Attack2 = 10,
	Attack3 = 11,
	Attack4 = 12,
	AttackRange1 = 14,
	AttackRange2 = 15,
	AttackRange3 = 16,
	Special = 17,
	Struck = 18,
	Harvest = 19,
	Spell = 20,
	Die = 21,
	Dead = 22,
	Appear = 27,
	Revive = 28,
	SitDown = 29,
	Mine = 30,
	Sneek = 31,
	DashAttack = 32,
	Lunge = 33,
	WalkingBow = 34,
	RunningBow = 35,
	Jump = 36,
	MountStanding = 37,
	MountWalking = 38,
	MountRunning = 39,
	MountStruck = 40,
	MountAttack = 41,
	FishingCast = 42,
	FishingWait = 43,
	FishingReel = 44
};

/** Core combat/character stats. Values mirror Stat in Shared/Data/Stat.cs. */
UENUM(BlueprintType)
enum class EMirStat : uint8
{
	MinAC = 0,
	MaxAC = 1,
	MinMAC = 2,
	MaxMAC = 3,
	MinDC = 4,
	MaxDC = 5,
	MinMC = 6,
	MaxMC = 7,
	MinSC = 8,
	MaxSC = 9,
	Accuracy = 10,
	Agility = 11,
	HP = 12,
	MP = 13,
	AttackSpeed = 14,
	Luck = 15,
	BagWeight = 16,
	HandWeight = 17,
	WearWeight = 18,
	Reflect = 19,
	Strong = 20,
	Holy = 21,
	Freezing = 22,
	PoisonAttack = 23,
	MagicResist = 30,
	PoisonResist = 31,
	HealthRecovery = 32,
	SpellRecovery = 33,
	PoisonRecovery = 34,
	CriticalRate = 35,
	CriticalDamage = 36
};

/** Poison/status flags, mirroring the [Flags] PoisonType enum. */
UENUM(BlueprintType, meta = (Bitflags))
enum class EMirPoison : uint32
{
	None = 0,
	Green = 1,
	Red = 2,
	Slow = 4,
	Frozen = 8,
	Stun = 16,
	Paralysis = 32,
	DelayedExplosion = 64,
	Bleeding = 128,
	LRParalysis = 256,
	Blindness = 512,
	Dazed = 1024
};

/** A single animation clip definition, decoded from a .Lib FrameSet entry. */
USTRUCT(BlueprintType)
struct FMirAnimFrame
{
	GENERATED_BODY()

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Mir")
	EMirAction Action = EMirAction::Standing;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Mir")
	int32 Start = 0;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Mir")
	int32 Count = 1;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Mir")
	int32 Skip = 0;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Mir")
	int32 Interval = 100;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Mir")
	bool bReverse = false;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Mir")
	bool bBlend = false;
};

namespace MirIso
{
	/** Cell dimensions of the original isometric grid (GameScene.cs). */
	constexpr int32 CellWidth = 48;
	constexpr int32 CellHeight = 32;

	/** Direction as a tile-space step (x grows right, y grows down). */
	inline FIntPoint DirectionToTileStep(EMirDirection Direction)
	{
		static const FIntPoint Steps[8] = {
			FIntPoint(0, -1),  // Up
			FIntPoint(1, -1),  // UpRight
			FIntPoint(1, 0),   // Right
			FIntPoint(1, 1),   // DownRight
			FIntPoint(0, 1),   // Down
			FIntPoint(-1, 1),  // DownLeft
			FIntPoint(-1, 0),  // Left
			FIntPoint(-1, -1)  // UpLeft
		};
		return Steps[static_cast<uint8>(Direction) & 7];
	}

	/** Convert a world-space delta to the nearest of the 8 MirDirections. */
	inline EMirDirection WorldDeltaToDirection(const FVector2D& Delta)
	{
		// Mirror of the original: angle 0 = Up, clockwise in screen space.
		const float Angle = FMath::Atan2(Delta.X, -Delta.Y); // 0 at Up
		const float Sector = FMath::DegreesToRadians(22.5f);
		int32 Index = FMath::RoundToInt(Angle / (PI / 4.0f));
		Index = ((Index % 8) + 8) % 8;
		(void)Sector;
		return static_cast<EMirDirection>(Index);
	}

	/** Isometric screen offset for a tile coordinate (matches the 48x32 grid). */
	inline FVector2D TileToIso(const FIntPoint& Tile)
	{
		return FVector2D(
			(Tile.X - Tile.Y) * (CellWidth * 0.5f),
			(Tile.X + Tile.Y) * (CellHeight * 0.5f));
	}
}
