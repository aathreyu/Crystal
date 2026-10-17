// Server-authoritative game mode: wires the Mir pawn/controller/state classes
// and spawns the tile map on the dedicated server.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/GameModeBase.h"
#include "MirGameMode.generated.h"

class AMirMapActor;

UCLASS()
class MIRUE_API AMirGameMode : public AGameModeBase
{
	GENERATED_BODY()

public:
	AMirGameMode();

	virtual void BeginPlay() override;
	virtual void PostLogin(APlayerController* NewPlayer) override;

	/** Converted map JSON to load on start (relative to Content, or absolute). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadWrite, Category = "Mir")
	FString StartupMapJson = TEXT("MirData/Maps/0.json");

	UPROPERTY(BlueprintReadOnly, Category = "Mir")
	TObjectPtr<AMirMapActor> MapActor;

private:
	void SpawnMap();
};
