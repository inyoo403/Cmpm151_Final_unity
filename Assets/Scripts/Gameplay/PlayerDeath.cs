using System.Collections;
using System.Collections.Generic;
using Platformer.Audio;
using Platformer.Core;
using Platformer.Model;
using UnityEngine;

namespace Platformer.Gameplay
{
    /// <summary>
    /// Fired when the player has died.
    /// </summary>
    /// <typeparam name="PlayerDeath"></typeparam>
    public class PlayerDeath : Simulation.Event<PlayerDeath>
    {
        PlatformerModel model = Simulation.GetModel<PlatformerModel>();

        public override void Execute()
        {
            var player = model.player;
            if (player.health.IsAlive)
            {
                player.health.Die();
                model.virtualCamera.Follow = null;
                model.virtualCamera.LookAt = null;
                // player.collider.enabled = false;
                player.controlEnabled = false;

                AudioManager.Instance.EmitSfx("player/hurt");
                player.animator.SetTrigger("hurt");
                player.animator.SetBool("dead", true);
                AudioManager.Instance.UpdatePlayerState(player);
                Simulation.Schedule<PlayerSpawn>(2);
            }
        }
    }
}
