using extOSC;
using Platformer.Mechanics;
using UnityEngine;

namespace Platformer.Audio
{
    /// <summary>
    /// Centralizes audio events and forwards them to Pure Data through OSC.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        static AudioManager s_Instance;

        [Header("OSC")]
        [SerializeField] string remoteHost = "127.0.0.1";
        [SerializeField] int remotePort = 9000;

        [Header("Unity Audio")]
        [SerializeField] bool muteUnityAudio = true;

        [Header("Debug")]
        [SerializeField] bool logOscMessages = true;

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

            if (logOscMessages)
                Debug.Log($"AudioManager initialized. OSC target: {remoteHost}:{remotePort}");
        }

        void OnDestroy()
        {
            if (s_Instance == this)
                s_Instance = null;
        }

        public void EmitSfx(string eventName)
        {
            if (string.IsNullOrWhiteSpace(eventName))
            {
                Debug.LogWarning("Tried to send an empty SFX event.");
                return;
            }

            if (transmitter == null)
            {
                Debug.LogWarning($"OSC transmitter is null. Failed to send SFX: {eventName}");
                return;
            }

            string normalizedEventName = NormalizeSfxName(eventName);

            if (logOscMessages)
                Debug.Log($"Sending OSC SFX: {eventName} -> {normalizedEventName}");

            var message = new OSCMessage("/sfx");
            message.AddValue(OSCValue.String(normalizedEventName));
            transmitter.Send(message);
        }

        public void EmitSfx(string eventName, float value)
        {
            if (string.IsNullOrWhiteSpace(eventName))
            {
                Debug.LogWarning("Tried to send an empty SFX event.");
                return;
            }

            if (transmitter == null)
            {
                Debug.LogWarning($"OSC transmitter is null. Failed to send SFX: {eventName}");
                return;
            }

            string normalizedEventName = NormalizeSfxName(eventName);
            float normalizedValue = Mathf.Clamp01(value);

            if (logOscMessages)
                Debug.Log($"Sending OSC SFX: {eventName} -> {normalizedEventName}, value: {normalizedValue}");

            var message = new OSCMessage("/sfx");
            message.AddValue(OSCValue.String(normalizedEventName));
            message.AddValue(OSCValue.Float(normalizedValue));
            transmitter.Send(message);
        }

        public void EmitAnimationClip(AudioClip clip)
        {
            if (clip == null)
                return;

            string mappedEvent = MapClipToEvent(clip.name);

            if (logOscMessages)
                Debug.Log($"AudioClip detected: {clip.name} -> mapped to: {mappedEvent}");

            EmitSfx(mappedEvent);

            if (transmitter == null)
                return;

            var clipMessage = new OSCMessage("/sfx/clip");
            clipMessage.AddValue(OSCValue.String(clip.name));
            transmitter.Send(clipMessage);
        }

        public void UpdatePlayerState(PlayerController player)
        {
            if (player == null)
                return;

            if (transmitter == null)
            {
                Debug.LogWarning("OSC transmitter is null. Failed to send player state.");
                return;
            }

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
            if (string.IsNullOrWhiteSpace(key))
            {
                Debug.LogWarning("Tried to send an empty game state key.");
                return;
            }

            if (transmitter == null)
            {
                Debug.LogWarning($"OSC transmitter is null. Failed to send game state: {key}");
                return;
            }

            if (logOscMessages)
                Debug.Log($"Sending OSC Game State: {key} = {value}");

            var message = new OSCMessage("/state/game");
            message.AddValue(OSCValue.String(key));
            message.AddValue(OSCValue.Int(value ? 1 : 0));
            transmitter.Send(message);
        }

        string NormalizeSfxName(string eventName)
        {
            string normalized = eventName.Trim().ToLowerInvariant();

            normalized = normalized.Replace("\\", "/");
            normalized = normalized.Replace("_", "");
            normalized = normalized.Replace("-", "");

            switch (normalized)
            {
                case "player/footstep":
                case "player/walk":
                case "footstep":
                case "walk":
                    return "walk";

                case "player/jump":
                case "jump":
                    return "jump";

                case "player/land":
                case "player/landonground":
                case "landonground":
                case "land":
                    return "land";

                case "player/hurt":
                case "hurt":
                    return "hurt";

                case "player/death":
                case "player/respawn":
                case "death":
                case "respawn":
                    return "death";

                case "enemy/landedon":
                case "enemy/landed/on":
                case "enemy/landonenemy":
                case "player/landonenemy":
                case "landonenemy":
                case "landenemy":
                    return "landenemy";

                case "token/collect":
                case "collect":
                case "coin":
                    return "collect";

                case "music/theme":
                case "music":
                case "theme":
                    return "music";

                default:
                    return MapClipToEvent(normalized);
            }
        }

        string MapClipToEvent(string clipName)
        {
            string normalized = clipName.Trim().ToLowerInvariant();

            normalized = normalized.Replace("\\", "/");
            normalized = normalized.Replace("_", "");
            normalized = normalized.Replace("-", "");

            if (normalized.Contains("walk") || normalized.Contains("footstep"))
                return "walk";

            if (normalized.Contains("jump"))
                return "jump";

            if (normalized.Contains("landonenemy") ||
                normalized.Contains("landedon") ||
                normalized.Contains("landenemy"))
                return "landenemy";

            if (normalized.Contains("land"))
                return "land";

            if (normalized.Contains("hurt") || normalized.Contains("hit") || normalized.Contains("damage"))
                return "hurt";

            if (normalized.Contains("death") || normalized.Contains("die") || normalized.Contains("respawn"))
                return "death";

            if (normalized.Contains("collect") || normalized.Contains("token") || normalized.Contains("coin"))
                return "collect";

            if (normalized.Contains("music") || normalized.Contains("theme"))
                return "music";

            return normalized;
        }
    }
}
