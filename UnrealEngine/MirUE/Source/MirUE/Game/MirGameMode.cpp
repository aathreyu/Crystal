#include "Game/MirGameMode.h"

#include "Game/MirGameState.h"
#include "Game/MirPlayerState.h"
#include "Player/MirCharacter.h"
#include "Player/MirPlayerController.h"
#include "World/MirMapActor.h"
#include "Engine/World.h"
#include "Misc/Paths.h"

AMirGameMode::AMirGameMode()
{
	DefaultPawnClass = AMirCharacter::StaticClass();
	PlayerControllerClass = AMirPlayerController::StaticClass();
	PlayerStateClass = AMirPlayerState::StaticClass();
	GameStateClass = AMirGameState::StaticClass();
}

void AMirGameMode::BeginPlay()
{
	Super::BeginPlay();

	SpawnMap();

	if (AMirGameState* GS = GetGameState<AMirGameState>())
	{
		GS->MapName = FPaths::GetBaseFilename(StartupMapJson);
	}
}

void AMirGameMode::SpawnMap()
{
	UWorld* World = GetWorld();
	if (!World)
	{
		return;
	}

	FActorSpawnParameters Params;
	Params.SpawnCollisionHandlingOverride =
		ESpawnActorCollisionHandlingMethod::AlwaysSpawn;

	MapActor = World->SpawnActor<AMirMapActor>(AMirMapActor::StaticClass(),
		FTransform::Identity, Params);

	if (!MapActor)
	{
		return;
	}

	// Accept either an absolute path or one relative to the project content dir.
	FString JsonPath = StartupMapJson;
	if (FPaths::IsRelative(JsonPath))
	{
		JsonPath = FPaths::ProjectContentDir() / StartupMapJson;
	}

	MapActor->LoadMapFromJson(JsonPath);
}

void AMirGameMode::PostLogin(APlayerController* NewPlayer)
{
	Super::PostLogin(NewPlayer);

	if (AMirPlayerState* PS = NewPlayer ? NewPlayer->GetPlayerState<AMirPlayerState>() : nullptr)
	{
		PS->CharacterName = PS->GetPlayerName();
		PS->Level = 1;
		PS->CurrentHP = 100;
		PS->CurrentMP = 50;
	}
}
