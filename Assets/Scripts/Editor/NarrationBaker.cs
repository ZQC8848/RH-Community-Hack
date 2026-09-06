using System;
using System.IO;
using System.Net.Http;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using RHCommunityHack.Play;

namespace RHCommunityHack.EditorTools
{
    // Records the prologue narration: each PrologueCue's subtitle becomes a voice clip through
    // ElevenLabs text-to-speech, saved under Assets/Audio/Narration and assigned to the cue.
    //
    // IT RECORDS EACH LINE EXACTLY ONCE. This is an editor tool, not something the game does at
    // runtime, and it is deliberately hard to make it spend twice:
    //
    //     cue already has a clip        -> untouched (only warns if the subtitle changed since)
    //     clip file already on disk     -> assigned, no request made
    //     neither                       -> ONE request, file written, clip assigned
    //
    // So re-running the menu after the first time costs nothing, and the way to re-record a line
    // is explicit: delete its .mp3 and run again. The subtitle that produced each clip is kept in
    // a .txt beside it, which is how the tool knows a line has been rewritten under a stale clip.
    //
    // THE API KEY IS NEVER IN THE PROJECT. It is read from the ELEVENLABS_API_KEY environment
    // variable, or failing that from .ai/secrets/elevenlabs.key, which .gitignore excludes. Do not
    // move it into a const, a ScriptableObject or the scene - all three end up in the repository.
    //
    // Every cue's text is sent with the lines before and after it as previous_text / next_text.
    // The script's second narrator block is split across three cues so that the AR glasses, the
    // cameras and the tower can appear as each is named; without that context the voice would
    // land a full stop on "Augmented Reality glasses," and the join would be audible.
    public static class NarrationBaker
    {
        const string Menu = "RH Community Hack/Narration/";
        const string OutputFolder = "Assets/Audio/Narration";

        const string KeyEnv = "ELEVENLABS_API_KEY";
        const string KeyFile = ".ai/secrets/elevenlabs.key";

        // "Matilda" - a warm, unhurried American female voice, the closest of the account's
        // default library to the script's narrator. Changing this only affects lines recorded
        // from now on; to re-voice the whole prologue, delete the folder and run again.
        const string VoiceId = "XrExE9yKIg1WjnnlVkGX";
        const string ModelId = "eleven_multilingual_v2";
        const string OutputFormat = "mp3_44100_128";
        const string Endpoint = "https://api.elevenlabs.io/v1/text-to-speech/";

        [MenuItem(Menu + "Generate Missing Voice Clips")]
        static void GenerateMissing() => Run(allowRequests: true);

        // Same walk, no network: wires up whatever is already on disk. For a clone of the repo
        // that has the clips but not the key, or after a cue's reference was lost.
        [MenuItem(Menu + "Assign Existing Clips Only")]
        static void AssignExisting() => Run(allowRequests: false);

        static void Run(bool allowRequests)
        {
            var directors = UnityEngine.Object.FindObjectsByType<PrologueDirector>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (directors.Length == 0)
            {
                Debug.LogWarning("[NarrationBaker] No PrologueDirector in the open scenes.");
                return;
            }

            string key = null;
            if (allowRequests)
            {
                key = LoadKey();
                if (key == null) return;   // LoadKey has said why
            }

            int generated = 0, reused = 0, kept = 0, skipped = 0, failed = 0;

            try
            {
                foreach (var director in directors)
                {
                    var so = new SerializedObject(director);
                    var cues = so.FindProperty("cues");

                    for (int i = 0; i < cues.arraySize; i++)
                    {
                        var cue = cues.GetArrayElementAtIndex(i);
                        var voice = cue.FindPropertyRelative("voice");
                        string label = cue.FindPropertyRelative("label").stringValue;
                        string text = cue.FindPropertyRelative("subtitle").stringValue.Trim();
                        string path = ClipPath(i, label);

                        if (voice.objectReferenceValue != null)
                        {
                            kept++;
                            WarnIfStale(path, text, i, label);
                            continue;
                        }

                        if (text.Length == 0) { skipped++; continue; }   // a silent beat

                        if (!File.Exists(Absolute(path)))
                        {
                            if (!allowRequests)
                            {
                                Debug.Log($"[NarrationBaker] Cue {i} '{label}' has no clip on disk; " +
                                          "run Generate Missing Voice Clips to record it.");
                                skipped++;
                                continue;
                            }

                            EditorUtility.DisplayProgressBar("Recording narration",
                                $"Cue {i}: {label}", (i + 0.5f) / cues.arraySize);

                            string previous = i > 0
                                ? cues.GetArrayElementAtIndex(i - 1).FindPropertyRelative("subtitle").stringValue.Trim()
                                : "";
                            string next = i + 1 < cues.arraySize
                                ? cues.GetArrayElementAtIndex(i + 1).FindPropertyRelative("subtitle").stringValue.Trim()
                                : "";

                            byte[] audio = Synthesize(key, text, previous, next, out string error);
                            if (audio == null)
                            {
                                Debug.LogError($"[NarrationBaker] Cue {i} '{label}' failed: {error}");
                                failed++;
                                continue;
                            }

                            Directory.CreateDirectory(Absolute(OutputFolder));
                            File.WriteAllBytes(Absolute(path), audio);
                            File.WriteAllText(Absolute(SidecarPath(path)), text, new UTF8Encoding(false));
                            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                            AssetDatabase.ImportAsset(SidecarPath(path));
                            generated++;
                        }
                        else
                        {
                            reused++;
                            WarnIfStale(path, text, i, label);
                        }

                        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                        if (clip == null)
                        {
                            Debug.LogError($"[NarrationBaker] {path} exists but did not import as an AudioClip.");
                            failed++;
                            continue;
                        }

                        voice.objectReferenceValue = clip;
                    }

                    if (so.ApplyModifiedProperties())
                        EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log($"[NarrationBaker] Recorded {generated}, assigned {reused} from disk, " +
                      $"left {kept} already wired, skipped {skipped}, failed {failed}. " +
                      (generated + reused > 0 ? "Save the scene to keep the assignments." : ""));
        }

        // ---------------------------------------------------------------------------------------

        static string ClipPath(int index, string label) => $"{OutputFolder}/{index:00}-{Slug(label)}.mp3";
        static string SidecarPath(string clipPath) => Path.ChangeExtension(clipPath, ".txt");
        static string Absolute(string assetPath) => Path.Combine(Directory.GetCurrentDirectory(), assetPath);

        static string Slug(string label)
        {
            var sb = new StringBuilder();
            foreach (char c in (label ?? "").ToLowerInvariant())
                sb.Append(char.IsLetterOrDigit(c) ? c : '-');
            string s = sb.ToString().Trim('-');
            while (s.Contains("--")) s = s.Replace("--", "-");
            return s.Length > 0 ? s : "cue";
        }

        // A clip is stale when its subtitle has been edited since it was recorded. Not fatal -
        // the old audio still plays - but the words on screen no longer match the voice, so say
        // so, and say exactly what to do about it.
        static void WarnIfStale(string clipPath, string text, int index, string label)
        {
            string sidecar = Absolute(SidecarPath(clipPath));
            if (!File.Exists(sidecar)) return;
            if (File.ReadAllText(sidecar).Trim() == text) return;
            Debug.LogWarning($"[NarrationBaker] Cue {index} '{label}': subtitle has changed since " +
                             $"{clipPath} was recorded. Delete that file and run Generate Missing " +
                             "Voice Clips to re-record it.");
        }

        static string LoadKey()
        {
            // Fully qualified: inside RHCommunityHack, `Environment` is our own namespace.
            string key = System.Environment.GetEnvironmentVariable(KeyEnv);
            if (string.IsNullOrWhiteSpace(key))
            {
                string file = Absolute(KeyFile);
                if (File.Exists(file)) key = File.ReadAllText(file);
            }

            key = key?.Trim();
            if (!string.IsNullOrEmpty(key)) return key;

            Debug.LogError($"[NarrationBaker] No ElevenLabs key. Set {KeyEnv}, or put the key on its " +
                           $"own in {KeyFile} (that folder is gitignored). Nothing was requested.");
            return null;
        }

        static byte[] Synthesize(string key, string text, string previous, string next, out string error)
        {
            var body = new StringBuilder();
            body.Append("{\"text\":").Append(Quote(text));
            body.Append(",\"model_id\":").Append(Quote(ModelId));
            if (previous.Length > 0) body.Append(",\"previous_text\":").Append(Quote(previous));
            if (next.Length > 0) body.Append(",\"next_text\":").Append(Quote(next));
            body.Append(",\"voice_settings\":{\"stability\":0.5,\"similarity_boost\":0.75,\"style\":0.0,\"use_speaker_boost\":true}");
            body.Append('}');

            try
            {
                // Synchronous on purpose: a menu command that returns before the file exists
                // would leave the cue unassigned and invite a second click, and a second click
                // is a second bill.
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
                client.DefaultRequestHeaders.Add("xi-api-key", key);
                client.DefaultRequestHeaders.Add("Accept", "audio/mpeg");

                using var content = new StringContent(body.ToString(), Encoding.UTF8, "application/json");
                using var response = client.PostAsync($"{Endpoint}{VoiceId}?output_format={OutputFormat}", content).Result;

                byte[] bytes = response.Content.ReadAsByteArrayAsync().Result;
                if (!response.IsSuccessStatusCode)
                {
                    error = $"HTTP {(int)response.StatusCode}: {Encoding.UTF8.GetString(bytes)}";
                    return null;
                }

                error = null;
                return bytes;
            }
            catch (Exception e)
            {
                error = e.GetBaseException().Message;
                return null;
            }
        }

        static string Quote(string s)
        {
            var sb = new StringBuilder(s.Length + 2);
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}
