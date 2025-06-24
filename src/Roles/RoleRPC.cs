namespace Lotus.Roles;

public enum RoleRPC: uint
{
    /// <summary>
    /// Clears the target player id's body
    /// </summary>
    /// <param name="playerId">the player id for the body to clear</param>
    RemoveBody = 587,
    /// <summary>
    /// Starts/Stop the reactor flash effect
    /// </summary>
    /// <param name="finish"><see cref="bool"/> if true, stops the flash, otherwise starts it</param>
    ReactorFlash,
    /// <summary>
    /// Calls <see cref="Lotus.Roles.GUI.RoleUIManager"/>'s <i>RevertEverything</i> method on the client that receives this.
    /// </summary>
    RevertRoleUI,
    /// <summary>
    /// Calls <see cref="Lotus.Roles.GUI.RoleUIManager"/>'s <i>ForceUpdate</i> method on the client that receives this.
    /// </summary>
    ForceRoleUIUpdate,
}