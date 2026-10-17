// Replicated per-player state: character identity + core stats.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/PlayerState.h"
#include "Core/MirTypes.h"
#include "MirPlayerState.generated.h"

UCLASS()
class MIRUE_API AMirPlayerState : public APlayerState
{
	GENERATED_BODY()

public:
	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;

	UPROPERTY(Replicated, BlueprintReadOnly, Category = "Mir")
	FString CharacterName;

	UPROPERTY(Replicated, BlueprintReadOnly, Category = "Mir")
	EMirClass Class = EMirClass::Warrior;

	UPROPERTY(Replicated, BlueprintReadOnly, Category = "Mir")
	EMirGender Gender = EMirGender::Male;

	UPROPERTY(Replicated, BlueprintReadOnly, Category = "Mir")
	int32 Level = 1;

	UPROPERTY(Replicated, BlueprintReadOnly, Category = "Mir")
	int64 Experience = 0;

	UPROPERTY(Replicated, BlueprintReadOnly, Category = "Mir")
	int32 CurrentHP = 0;

	UPROPERTY(Replicated, BlueprintReadOnly, Category = "Mir")
	int32 CurrentMP = 0;

	UPROPERTY(Replicated, BlueprintReadOnly, Category = "Mir")
	EMirAttackMode AttackMode = EMirAttackMode::Peace;

	/** Optional override for the body sprite library (e.g. class armour library). */
	UPROPERTY(Replicated, BlueprintReadOnly, Category = "Mir")
	FName BodyLibrary = TEXT("Monsters");

	UPROPERTY(Replicated, BlueprintReadOnly, Category = "Mir")
	int32 BodyIndex = 0;
};
