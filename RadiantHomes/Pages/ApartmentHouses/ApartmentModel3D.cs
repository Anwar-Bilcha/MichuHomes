using System.Buffers.Binary;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using RadiantHomes.Utilities;

namespace RadiantHomes.Pages.ApartmentHouses
{
    /// <summary>
    /// An apartment's optional 3D model ("3D live view"): one GLB file (glTF 2.0 binary) per apartment.
    /// </summary>
    /// <remarks>
    /// Kept on disk like the apartment photos (see <see cref="ApartmentHouseForm"/>), so no database column is needed:
    /// wwwroot/uploads/apartmentmodels/{apartmentId}/model_{32 hex}.glb. Every upload gets a new file name, so a
    /// replaced model never reaches a browser from its cache and a download in progress is never cut off.
    /// Uploaded on the apartment Create/Edit pages (_ApartmentModelField), shown on the Details pages
    /// (_ApartmentModelViewer + wwwroot/js/model-view.js), removed with the apartment.
    /// The walk-through rooms (named spots inside the model, set up on ApartmentHouses/Tour) are kept as rooms.json
    /// in the same folder.
    /// </remarks>
    public static class ApartmentModel3D
    {
        public const long MaxBytes = 50L * 1024 * 1024;

        /// <summary>Request limit for the forms that can carry a model (plus photos): above Kestrel's 30 MB default.</summary>
        public const long MaxRequestBytes = 200L * 1024 * 1024;

        /// <summary>Form field names used by _ApartmentModelField.</summary>
        public const string FileField = "Model3D";
        public const string RemoveField = "RemoveModel3D";

        private const string FolderName = "apartmentmodels";

        public sealed record StoredModel(string Url, long Bytes)
        {
            public string Size => FormatSize(Bytes);
        }

        /// <summary>
        /// A room of the walk-through: where a visitor stands (a floor point, model coordinates in metres) and the
        /// direction they look first (camera orbit angles in radians: <see cref="Theta"/> around, <see cref="Phi"/> from straight up).
        /// </summary>
        public sealed record TourRoom(string Name, double X, double Y, double Z, double Theta, double Phi);

        /// <summary>What the pages need to show the viewer. <paramref name="Editor"/>: the rooms editor (loads at once, no cover).</summary>
        public sealed record Viewer(StoredModel Model, string Title, string? PosterUrl, IReadOnlyList<TourRoom> Rooms, bool Editor = false);

        /// <summary>What the form field (_ApartmentModelField) shows: the current model and, for an existing apartment, its rooms.</summary>
        public sealed record Field(StoredModel? Model, Guid? ApartmentId = null, int RoomCount = 0);

        public const int MaxRooms = 40;
        public const int MaxRoomNameLength = 60;
        private const string RoomsFile = "rooms.json";
        private static readonly JsonSerializerOptions RoomsJson = new(JsonSerializerDefaults.Web) { WriteIndented = true };

        /// <summary>The apartment's model, or null when it has none.</summary>
        public static StoredModel? Find(IWebHostEnvironment env, Guid apartmentId)
        {
            var folder = ModelFolder(env, apartmentId);
            if (!Directory.Exists(folder)) return null;

            var file = new DirectoryInfo(folder).GetFiles("*.glb").OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault();
            return file is null ? null : new StoredModel($"/uploads/{FolderName}/{apartmentId:D}/{file.Name}", file.Length);
        }

        /// <summary>Ids (among <paramref name="apartmentIds"/>) of the apartments that have a model — for listing badges.</summary>
        public static HashSet<Guid> WithModels(IWebHostEnvironment env, IEnumerable<Guid> apartmentIds) =>
            apartmentIds.Where(id => Find(env, id) != null).ToHashSet();

        /// <summary>
        /// The model posted by the form (field <see cref="FileField"/>), or null when none was chosen or it is invalid
        /// (then the reason is added to <paramref name="modelState"/> under <see cref="FileField"/>).
        /// </summary>
        public static async Task<IFormFile?> CollectAsync(HttpRequest request, ModelStateDictionary modelState)
        {
            if (!request.HasFormContentType) return null;
            var file = request.Form.Files.GetFile(FileField);
            if (file is null || file.Length == 0) return null;

            var error = await ValidateAsync(file);
            if (error is null) return file;
            modelState.AddModelError(FileField, error);
            return null;
        }

        /// <summary>Whether the form's "Remove the 3D model" box was ticked.</summary>
        public static bool RemoveRequested(HttpRequest request) =>
            request.HasFormContentType && string.Equals(request.Form[RemoveField], "true", StringComparison.OrdinalIgnoreCase);

        /// <summary>Returns an error message, or null when the file is a GLB model we accept.</summary>
        public static async Task<string?> ValidateAsync(IFormFile file)
        {
            if (!string.Equals(Path.GetExtension(file.FileName), ".glb", StringComparison.OrdinalIgnoreCase))
                return $"\"{file.FileName}\" is not a 3D model in GLB format (.glb).";
            if (file.Length > MaxBytes)
                return $"\"{file.FileName}\" is larger than {MaxBytes / (1024 * 1024)} MB.";

            // GLB header (little-endian): magic "glTF", container version 2, total length of the file.
            // This turns away renamed files and truncated uploads before anything is saved.
            var header = new byte[12];
            await using (var stream = file.OpenReadStream())
            {
                var read = 0;
                while (read < header.Length)
                {
                    var n = await stream.ReadAsync(header.AsMemory(read));
                    if (n == 0) break;
                    read += n;
                }
                if (read < header.Length) return InvalidModel(file);
            }
            var isGlb = BinaryPrimitives.ReadUInt32LittleEndian(header) == 0x46546C67 // "glTF"
                        && BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4)) == 2
                        && BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(8)) == file.Length;
            return isGlb ? null : InvalidModel(file);
        }

        private static string InvalidModel(IFormFile file) =>
            $"\"{file.FileName}\" is not a valid GLB 3D model. Export it again as glTF 2.0 binary (.glb).";

        /// <summary>
        /// Applies the form once the apartment is saved: a new file replaces the model, otherwise a ticked
        /// "remove" box deletes it. Returns the note for the success message, or an empty string.
        /// </summary>
        public static async Task<string> ApplyAsync(IWebHostEnvironment env, Guid apartmentId, IFormFile? upload, bool remove)
        {
            var had = Find(env, apartmentId) is not null;
            if (upload is not null)
            {
                await SaveAsync(env, apartmentId, upload);
                return had ? "3D model replaced." : "3D model added.";
            }
            if (remove && had)
            {
                Delete(env, apartmentId);
                return "3D model removed.";
            }
            return string.Empty;
        }

        public static async Task SaveAsync(IWebHostEnvironment env, Guid apartmentId, IFormFile file)
        {
            var folder = ModelFolder(env, apartmentId);
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, $"model_{Guid.NewGuid():N}.glb");
            await using (var stream = new FileStream(path, FileMode.CreateNew))
            {
                await file.CopyToAsync(stream);
            }

            // Older versions: removed now, or left for the next save if a visitor is still downloading one.
            foreach (var old in Directory.GetFiles(folder, "*.glb").Where(f => !string.Equals(f, path, StringComparison.OrdinalIgnoreCase)))
            {
                TryDelete(old);
            }
        }

        /// <summary>Deletes the apartment's model (all versions). Safe to call when there is none.</summary>
        public static void Delete(IWebHostEnvironment env, Guid apartmentId)
        {
            var folder = ModelFolder(env, apartmentId);
            if (!Directory.Exists(folder)) return;
            foreach (var file in Directory.GetFiles(folder)) TryDelete(file);
            try { Directory.Delete(folder); } catch (IOException) { /* a file is still in use; the folder stays */ }
        }

        // ----- Rooms of the walk-through (rooms.json next to the model; kept when the model is replaced,
        //       removed with it) -----

        /// <summary>The apartment's tour rooms in visiting order (empty when none are set up).</summary>
        public static IReadOnlyList<TourRoom> LoadRooms(IWebHostEnvironment env, Guid apartmentId)
        {
            var path = Path.Combine(ModelFolder(env, apartmentId), RoomsFile);
            if (!File.Exists(path)) return Array.Empty<TourRoom>();
            try
            {
                return (IReadOnlyList<TourRoom>?)ParseRooms(File.ReadAllText(path), out _) ?? Array.Empty<TourRoom>();
            }
            catch (IOException)
            {
                return Array.Empty<TourRoom>();
            }
        }

        /// <summary>Checked rooms from JSON (the editor's post or rooms.json), or null and the reason.</summary>
        public static List<TourRoom>? ParseRooms(string? json, out string? error)
        {
            error = null;
            List<TourRoom>? rooms;
            try
            {
                rooms = JsonSerializer.Deserialize<List<TourRoom>>(string.IsNullOrWhiteSpace(json) ? "[]" : json, RoomsJson);
            }
            catch (JsonException)
            {
                error = "The rooms could not be read. Please try again.";
                return null;
            }

            rooms ??= new();
            if (rooms.Count > MaxRooms)
            {
                error = $"A 3D tour can have at most {MaxRooms} rooms.";
                return null;
            }

            var clean = new List<TourRoom>(rooms.Count);
            foreach (var room in rooms)
            {
                var name = (room?.Name ?? string.Empty).Trim();
                if (name.Length == 0)
                {
                    error = "Every room needs a name.";
                    return null;
                }
                if (name.Length > MaxRoomNameLength)
                {
                    error = $"Room names can be at most {MaxRoomNameLength} characters.";
                    return null;
                }
                if (new[] { room!.X, room.Y, room.Z, room.Theta, room.Phi }.Any(n => !double.IsFinite(n) || Math.Abs(n) > 100_000))
                {
                    error = "The rooms could not be read. Please try again.";
                    return null;
                }
                clean.Add(room with
                {
                    Name = name,
                    Theta = Math.IEEERemainder(room.Theta, 2 * Math.PI),
                    Phi = Math.Clamp(room.Phi, 0.05, Math.PI - 0.05),
                });
            }
            return clean;
        }

        public static void SaveRooms(IWebHostEnvironment env, Guid apartmentId, IReadOnlyList<TourRoom> rooms)
        {
            var folder = ModelFolder(env, apartmentId);
            var path = Path.Combine(folder, RoomsFile);
            if (rooms.Count == 0)
            {
                if (File.Exists(path)) File.Delete(path);
                return;
            }
            Directory.CreateDirectory(folder);
            File.WriteAllText(path, JsonSerializer.Serialize(rooms, RoomsJson));
        }

        /// <summary>Rooms as JSON for the page scripts (the same shape the editor posts back).</summary>
        public static string RoomsToJson(IReadOnlyList<TourRoom> rooms) => JsonSerializer.Serialize(rooms, RoomsJson);

        /// <summary>"12.4 MB", "850 KB".</summary>
        public static string FormatSize(long bytes) =>
            bytes >= 1024 * 1024
                ? $"{bytes / (1024d * 1024):0.#} MB"
                : $"{Math.Max(1, (bytes + 1023) / 1024)} KB";

        /// <summary>wwwroot/uploads/apartmentmodels/{apartmentId}</summary>
        private static string ModelFolder(IWebHostEnvironment env, Guid apartmentId) =>
            SafeUpload.Under(Path.Combine(env.WebRootPath, "uploads"), FolderName, apartmentId.ToString("D"));

        private static void TryDelete(string path)
        {
            try { File.Delete(path); }
            catch (IOException) { /* in use (being downloaded) */ }
            catch (UnauthorizedAccessException) { }
        }
    }
}
