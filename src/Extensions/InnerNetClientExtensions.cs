using Hazel;
using InnerNet;

namespace Lotus.Extensions;

public static class InnerNetClientExtensions
{
    public static void WriteSpawnMessage(this InnerNetClient client, InnerNetObject netObjParent, int ownerId, SpawnFlags flags, MessageWriter msg)
    {
        msg.StartMessage((byte)4);
        msg.WritePacked(netObjParent.SpawnId);
        msg.WritePacked(ownerId);
        msg.Write((byte)flags);
        InnerNetObject[] componentsInChildren = netObjParent.GetComponentsInChildren<InnerNetObject>();
        msg.WritePacked(componentsInChildren.Length);
        foreach (InnerNetObject innerNetObject in componentsInChildren)
        {
            innerNetObject.OwnerId = ownerId;
            innerNetObject.SpawnFlags = flags;
            if (innerNetObject.NetId == 0)
            {
                innerNetObject.NetId = client.NetIdCnt++;
                client.allObjects.TryAddNetObject(innerNetObject);
            }
            msg.WritePacked(innerNetObject.NetId);
            msg.StartMessage((byte)1);
            innerNetObject.Serialize(msg, initialState: true);
            msg.EndMessage();
        }
        msg.EndMessage();
    }
}