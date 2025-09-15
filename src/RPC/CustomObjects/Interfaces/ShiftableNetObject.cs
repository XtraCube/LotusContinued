using System.Linq;
using Lotus.API.Player;
using UnityEngine;
using VentLib.Utilities.Extensions;

namespace Lotus.RPC.CustomObjects.Interfaces;

public class ShiftableNetObject: CustomNetObject
{
    public byte VisibleId { get; }

    public ShiftableNetObject(string objectName, Vector2 position, byte visibleTo = byte.MaxValue)
    {
        VisibleId = visibleTo;
        if (visibleTo != 255) Players.GetAllPlayers().Where(p => p.PlayerId != visibleTo).ForEach(Hide);
        CreateNetObject(objectName, position);
    }
}