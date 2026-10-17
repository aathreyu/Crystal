// Replicated world state: which Mir map is active and global world info.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/GameStateBase.h"
#include "MirGameState.generated.h"

UCLASS()
class MIRUE_API AMirGameState : public AGameStateBase
{
	GENERATED_BODY()

public:
	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;

	/** Active map file name (e.g. "0"), matching the server's map list. */
	UPROPERTY(Replicated, BlueprintReadOnly, Category = "Mir")
	FString MapName;

	/** In-game world time (mirrors the engine's day/night cycle). */
	UPROPERTY(Replicated, BlueprintReadOnly, Category = "Mir")
	float WorldTimeSeconds = 0.f;
};
