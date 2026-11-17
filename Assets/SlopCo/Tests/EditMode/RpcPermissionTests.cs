using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Netcode;
using SlopCo.Audio;
using SlopCo.Gameplay;
using SlopCo.Items;
using SlopCo.Player;

namespace SlopCo.Tests.EditMode
{
    /// <summary>
    /// Pins who may call the server-bound RPCs. NGO's default InvokePermission is Everyone, so an RPC that acts for
    /// ONE player has to say Owner — otherwise any client can grab, throw, spend or talk on another player's
    /// behalf. NGO enforces the attribute on both ends (the sender throws, the server drops and logs), so the
    /// attribute itself is the policy, and this is what keeps a new RPC from quietly shipping without it.
    /// </summary>
    public class RpcPermissionTests
    {
        // Components of Player.prefab: each server RPC on them acts for that one player (its grab, its inventory,
        // its kick, its ping, its voice), so only that player's own client — or the host, for a bot — may send it.
        private static readonly Type[] PlayerOwned =
        {
            typeof(PlayerCarryController), typeof(PlayerInventory), typeof(KickAbility),
            typeof(PingEmoteController), typeof(PlayerVoice),
        };

        private const BindingFlags Declared =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        // [Rpc(SendTo.X)] keeps X only as a constructor argument.
        private static bool SendsToServer(MethodInfo m)
        {
            foreach (var d in m.GetCustomAttributesData())
                if (typeof(RpcAttribute).IsAssignableFrom(d.AttributeType) && d.ConstructorArguments.Count == 1
                    && d.ConstructorArguments[0].ArgumentType == typeof(SendTo))
                    return (SendTo)d.ConstructorArguments[0].Value == SendTo.Server;
            return false;
        }

        [Test]
        public void PlayerOwned_ServerRpcs_AreOwnerOnly()
        {
            var checkedRpcs = new List<string>();
            var open = new List<string>();
            foreach (var t in PlayerOwned)
                foreach (var m in t.GetMethods(Declared))
                {
                    var attr = m.GetCustomAttribute<RpcAttribute>();
                    if (attr == null || !SendsToServer(m)) continue;
                    checkedRpcs.Add($"{t.Name}.{m.Name}");
                    if (attr.InvokePermission != RpcInvokePermission.Owner) open.Add($"{t.Name}.{m.Name}");
                }

            // 3 carry (grab/release/throw) + 5 inventory + kick + ping + voice. A drop here means the reflection
            // stopped seeing them — not that they became safe.
            Assert.That(checkedRpcs.Count, Is.EqualTo(11), string.Join(", ", checkedRpcs));
            Assert.That(open, Is.Empty, "server RPCs any client could send for another player");
        }

        [Test]
        public void StartingARun_IsHostOnly()
        {
            var m = typeof(RoundManager).GetMethod(nameof(RoundManager.RequestStartRpc));
            Assert.That(m, Is.Not.Null);
            Assert.That(SendsToServer(m), Is.True);
            Assert.That(m.GetCustomAttribute<RpcAttribute>().InvokePermission, Is.EqualTo(RpcInvokePermission.Server));
        }
    }
}
