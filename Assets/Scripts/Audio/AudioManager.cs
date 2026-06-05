using extOSC;
using Platformer.Mechanics;
using UnityEngine;

namespace Platformer.Audio
{
    /// <summary>
    /// Centralizes audio events and forwards them to an external OSC client.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        static AudioManager s_Instance;

        [Header("OSC")]
        [SerializeField] string remoteHost = "127.0.0.1";
        [SerializeField] int remotePort = 9000;

        [Header("Unity Audio")]
        [SerializeField] bool muteUnityAudio = true;

        OSCTransmitter transmitter;

        public static AudioManager Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    s_Instance = FindFirstObjectByType<AudioManager>();
                    if (s_Instance == null)
                    {
                        var go = new GameObject("AudioManager");
                        s_Instance = go.AddComponent<AudioManager>();
                    }
                }

                return s_Instance;
            }
        }

        void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_Instance = this;
            DontDestroyOnLoad(gameObject);

            if (muteUnityAudio)
                AudioListener.volume = 0f;

            transmitter = GetComponent<OSCTransmitter>();
            if (transmitter == null)
                transmitter = gameObject.AddComponent<OSCTransmitter>();

            transmitter.RemoteHost = remoteHost;
            transmitter.RemotePort = remotePort;
            transmitter.Connect();
        }

        void OnDestroy()
        {
            if (s_Instance == this)
                s_Instance = null;
        }

        public void EmitSfx(string eventName)
        {
            var message = new OSCMessage("/sfx");
            message.AddValue(OSCValue.String(eventName));
            transmitter.Send(message);
        }

        public void EmitAnimationClip(AudioClip clip)
        {
            if (clip == null)
                return;

            EmitSfx(MapClipToEvent(clip.name));

            var message = new OSCMessage("/sfx/clip");
            message.AddValue(OSCValue.String(clip.name));
            transmitter.Send(message);
        }

        public void UpdatePlayerState(PlayerController player)
        {
            if (player == null)
                return;

            var message = new OSCMessage("/state/player");
            message.AddValue(OSCValue.Float(Mathf.Abs(player.velocity.x)));
            message.AddValue(OSCValue.Float(player.velocity.y));
            message.AddValue(OSCValue.Int(player.IsGrounded ? 1 : 0));
            message.AddValue(OSCValue.Int(player.health != null && player.health.IsAlive ? 1 : 0));
            message.AddValue(OSCValue.Int(player.controlEnabled ? 1 : 0));
            transmitter.Send(message);

            var jumpRatioMessage = new OSCMessage("/state/player/jump_ratio");
            jumpRatioMessage.AddValue(OSCValue.Float(player.JumpHoldRatio));
            transmitter.Send(jumpRatioMessage);
        }

        public void SetGameState(string key, bool value)
        {
            var message = new OSCMessage("/state/game");
            message.AddValue(OSCValue.String(key));
            message.AddValue(OSCValue.Int(value ? 1 : 0));
            transmitter.Send(message);
        }

        string MapClipToEvent(string clipName)
        {
            var normalized = clipName.ToLowerInvariant();

            if (normalized.Contains("walk"))
                return "player/footstep";
            if (normalized.Contains("landonenemy"))
                return "enemy/landed_on";
            if (normalized.Contains("land"))
                return "player/land";
            if (normalized.Contains("jump"))
                return "player/jump";
            if (normalized.Contains("hurt"))
                return "player/hurt";
            if (normalized.Contains("death"))
                return "player/respawn";
            if (normalized.Contains("collect"))
                return "token/collect";
            if (normalized.Contains("music"))
                return "music/theme";

            return $"clip/{normalized}";
        }
    }
}
