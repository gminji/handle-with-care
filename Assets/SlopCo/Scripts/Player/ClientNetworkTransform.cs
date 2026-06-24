using Unity.Netcode.Components;

namespace SlopCo.Player
{
    /// <summary>
    /// Owner-authoritative NetworkTransform for responsive local player movement. This is the
    /// version-stable NGO 2.x way to get client-authority (overriding OnIsServerAuthoritative) rather
    /// than relying on an inspector AuthorityMode enum that has varied across NGO versions.
    ///
    /// Consequence (documented): the server is NOT the authority on player position, so it cannot hard
    /// overwrite it without a snap-back fight. Movement bounds are therefore enforced softly on the
    /// owner (see PlayerController.EnforceBounds). Anti-cheat hardening is deferred past the slice.
    /// </summary>
    public class ClientNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
