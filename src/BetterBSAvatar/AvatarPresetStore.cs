using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BetterBSAvatar
{
    internal sealed class AvatarPresetStore
    {
        private const int MaximumFileBytes = 64 * 1024;
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            TypeNameHandling = TypeNameHandling.None,
            MaxDepth = 16,
            CheckAdditionalContent = true
        };
        private readonly string _directory;
        private readonly Action<string, string, string> _replaceFile;

        internal AvatarPresetStore(string directory, Action<string, string, string> replaceFile = null)
        {
            _directory = Path.GetFullPath(directory);
            _replaceFile = replaceFile ?? ((source, destination, backup) => File.Replace(source, destination, backup));
        }

        internal List<AvatarPreset> ReadAll(Action<string> warning = null)
        {
            List<AvatarPreset> presets = new List<AvatarPreset>();
            if (!Directory.Exists(_directory)) return presets;

            foreach (string file in Directory.GetFiles(_directory, "*.json"))
            {
                try
                {
                    AvatarPreset preset = Read(file);
                    presets.Add(preset);
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
                    exception is JsonException || exception is FormatException || exception is ArgumentException || exception is OverflowException)
                {
                    warning?.Invoke("Could not read " + Path.GetFileName(file) + ": " + exception.Message);
                }
            }

            return presets.OrderBy(preset => preset.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(preset => preset.Id).ToList();
        }

        internal static string NormalizeName(string name)
        {
            name = (name ?? string.Empty).Trim();
            if (name.Length < 1 || name.Length > 64 || AvatarAppearance.ContainsControlCharacter(name))
            {
                throw new ArgumentException("Use a name of 1–64 characters without line breaks or control characters.");
            }
            return name;
        }

        internal AvatarPreset Save(string name, AvatarAppearance appearance, Guid? overwriteId = null)
        {
            name = NormalizeName(name);
            if (appearance == null) throw new ArgumentNullException(nameof(appearance));
            appearance.Validate();
            List<AvatarPreset> presets = ReadAll();
            AvatarPreset previous = overwriteId.HasValue ? presets.FirstOrDefault(preset => preset.Id == overwriteId.Value) : null;
            if (overwriteId.HasValue && previous == null)
            {
                throw new IOException("That saved avatar is no longer available. Reopen the avatar menu and try again.");
            }
            EnsureUniqueName(presets, name, overwriteId);

            DateTimeOffset now = DateTimeOffset.UtcNow;
            AvatarPreset saved = new AvatarPreset
            {
                Id = previous != null ? previous.Id : Guid.NewGuid(),
                Name = name,
                CreatedAtUtc = previous != null ? previous.CreatedAtUtc : now,
                UpdatedAtUtc = now,
                GameVersion = "1.40.8",
                Appearance = appearance.Copy()
            };
            Write(saved, previous != null);
            return saved;
        }

        internal AvatarPreset Rename(Guid id, string name)
        {
            name = NormalizeName(name);
            List<AvatarPreset> presets = ReadAll();
            AvatarPreset preset = presets.FirstOrDefault(entry => entry.Id == id);
            if (preset == null) throw new IOException("That saved avatar is no longer available.");
            EnsureUniqueName(presets, name, id);
            preset.Name = name;
            preset.UpdatedAtUtc = DateTimeOffset.UtcNow;
            Write(preset, true);
            return preset;
        }

        internal void Delete(Guid id)
        {
            string file = PresetPath(id);
            // Never infer a filename from a display name or delete a backup automatically.
            if (!File.Exists(file)) throw new IOException("That saved avatar is no longer available.");
            File.Delete(file);
        }

        private AvatarPreset Read(string file)
        {
            if (new FileInfo(file).Length > MaximumFileBytes) throw new FormatException("Preset file is too large.");
            string json = File.ReadAllText(file, Encoding.UTF8);
            // Check the schema before interpreting a newer payload as the current format.
            JObject document;
            using (JsonTextReader reader = new JsonTextReader(new StringReader(json)) { MaxDepth = 16 })
            {
                document = JObject.Load(reader);
            }
            if (document["schemaVersion"]?.Type != JTokenType.Integer ||
                document.Value<int?>("schemaVersion") != AvatarPreset.CurrentSchemaVersion)
            {
                throw new FormatException("Unsupported preset version; the file has been left unchanged.");
            }
            AvatarPreset preset = JsonConvert.DeserializeObject<AvatarPreset>(json, JsonSettings);
            Guid filenameId;
            if (preset == null || preset.Id == Guid.Empty ||
                !Guid.TryParse(Path.GetFileNameWithoutExtension(file), out filenameId) || filenameId != preset.Id ||
                preset.Name != NormalizeName(preset.Name) || preset.Appearance == null ||
                string.IsNullOrEmpty(preset.GameVersion) || preset.CreatedAtUtc == default(DateTimeOffset) ||
                preset.UpdatedAtUtc < preset.CreatedAtUtc)
            {
                throw new FormatException("Invalid preset metadata.");
            }
            preset.Appearance.Validate();
            return preset;
        }

        private void Write(AvatarPreset preset, bool replace)
        {
            Directory.CreateDirectory(_directory);
            string file = PresetPath(preset.Id);
            string temporary = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                byte[] contents = new UTF8Encoding(false).GetBytes(JsonConvert.SerializeObject(preset, JsonSettings));
                using (FileStream stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(contents, 0, contents.Length);
                    stream.Flush(true);
                }
                if (replace) _replaceFile(temporary, file, file + ".bak");
                else File.Move(temporary, file);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        private string PresetPath(Guid id) => Path.Combine(_directory, id.ToString("N") + ".json");

        private static void EnsureUniqueName(IEnumerable<AvatarPreset> presets, string name, Guid? exceptId)
        {
            if (presets.Any(preset => preset.Id != exceptId && string.Equals(preset.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException("An avatar with that name already exists. Choose another name.");
            }
        }
    }
}
