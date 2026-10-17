#include "Game/MirGameState.h"
#include "Net/UnrealNetwork.h"

void AMirGameState::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);

	DOREPLIFETIME(AMirGameState, MapName);
	DOREPLIFETIME(AMirGameState, WorldTimeSeconds);
}
