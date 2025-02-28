using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;

namespace MetadataProcessor
{
    public static class MediaProcessor
    {
        // Unterstützte Dateiendungen
        private static readonly string[] ImageExtensions = { ".arw", ".gif", ".png", ".jpg", ".jpeg" };
        private static readonly string[] VideoExtensions = { ".mp4", ".mov", ".avi", ".mkv" };

        /// <summary>
        /// Durchsucht den Root-Ordner rekursiv nach Bild- und Videodateien.
        /// </summary>
        public static void ProcessRootFolder(string rootFolder)
        {
            var files = Directory.EnumerateFiles(rootFolder, "*.*", SearchOption.AllDirectories);
            foreach (string file in files)
            {
                if (IsImageFile(file))
                {
                    ProcessImage(file);
                }
                else if (IsVideoFile(file))
                {
                    ProcessVideo(file);
                }
            }
        }

        private static bool IsImageFile(string file)
        {
            string ext = Path.GetExtension(file).ToLowerInvariant();
            return ImageExtensions.Contains(ext);
        }

        private static bool IsVideoFile(string file)
        {
            string ext = Path.GetExtension(file).ToLowerInvariant();
            return VideoExtensions.Contains(ext);
        }

        /// <summary>
        /// Verarbeitet ein Bild: Liest das zugehörige JSON, extrahiert das Aufnahmedatum
        /// und aktualisiert die EXIF-Metadaten, wobei das Ergebnis als neue Datei gespeichert wird.
        /// </summary>
        public static void ProcessImage(string imagePath)
        {
            string jsonFile = GetJsonFilePath(imagePath);
            if (!File.Exists(jsonFile))
            {
                Console.WriteLine("No related JSON-File found for " + imagePath);
                return;
            }

            DateTime? dateTaken = ExtractDateFromJson(jsonFile);
            if (dateTaken.HasValue)
            {
                if (UpdateImageMetadata(imagePath, dateTaken.Value))
                {
                    // Wenn die .MP-Datei existiert, auch deren Metadaten aktualisieren
                    string mpFilePath = GetMPFilePath(imagePath);
                    if (!string.IsNullOrEmpty(mpFilePath) && File.Exists(mpFilePath))
                    {
                        if (UpdateMPMetadata(mpFilePath, dateTaken.Value))
                            Console.WriteLine("Metadata updated for MP file: " + mpFilePath);
                    }

                    // JSON-Datei löschen, nachdem die Metadaten erfolgreich aktualisiert wurden
                    File.Delete(jsonFile);
                }
            }
            else
            {
                Console.WriteLine("No date found in JSON-File for " + imagePath);
            }
        }

        /// <summary>
        /// Sucht nach einer .MP-Datei, die zur Bilddatei gehört. 
        /// Wenn die Bilddatei z.B. "image-example.MP.jpg" ist, wird die "image-example.MP" gesucht.
        /// </summary>
        private static string GetMPFilePath(string imagePath)
        {
            string mediaFileName = Path.GetFileNameWithoutExtension(imagePath);
            string mediaDir = Path.GetDirectoryName(imagePath);
            string mpFilePath = Path.Combine(mediaDir, mediaFileName);

            // Prüfen, ob die MP-Datei existiert
            if (File.Exists(mpFilePath))
                return mpFilePath;
            // Prüfen, ob die MP4-Datei existiert
            mpFilePath = Path.ChangeExtension(mpFilePath, ".MP4");
            if (File.Exists(mpFilePath))
                return mpFilePath;

            return null;
        }

        /// <summary>
        /// Aktualisiert die Metadaten einer .MP-Datei (vermutlich eine Textdatei oder ein anderes Format).
        /// Hier wird angenommen, dass es sich um eine einfache Textdatei handelt.
        /// </summary>
        private static bool UpdateMPMetadata(string mpFilePath, DateTime dateTaken)
        {
            bool success = true;
            try
            {
                // Da keine EXIF-Daten vorhanden sind, gehen wir davon aus, dass die .MP-Datei einfach die Dateiattribute benötigt.
                // Setze die Dateisystem-Metadaten
                File.SetCreationTime(mpFilePath, dateTaken);
                File.SetLastWriteTime(mpFilePath, dateTaken);
                Console.WriteLine("File-Attributes updated for MP file: " + mpFilePath);
            }
            catch (Exception ex)
            {
                success = false;
                Console.WriteLine("Error updating metadata for MP file " + mpFilePath + ": " + ex.Message);
            }
            return success;
        }

        /// <summary>
        /// Verarbeitet ein Video: Liest das zugehörige JSON, extrahiert das Erstellungsdatum
        /// und aktualisiert die Dateisystem-Metadaten (Creation- und LastWrite-Time).
        /// </summary>
        public static void ProcessVideo(string videoPath)
        {
            string jsonFile = GetJsonFilePath(videoPath);
            if (!File.Exists(jsonFile))
            {
                Console.WriteLine("No related JSON-File found for " + videoPath);
                return;
            }

            DateTime? dateTaken = ExtractDateFromJson(jsonFile);
            if (dateTaken.HasValue)
            {
                if (UpdateVideoMetadata(videoPath, dateTaken.Value))
                    File.Delete(jsonFile);
            }
            else
            {
                Console.WriteLine("No date found in JSON-File for " + videoPath);
            }
        }

        /// <summary>
        /// Ermittelt den Pfad zur JSON-Datei, die zum Medienfile gehört.
        /// Zunächst wird versucht, an den Medienpfad einfach ".json" anzuhängen,
        /// andernfalls wird die Dateiendung ersetzt.
        /// </summary>
        private static string GetJsonFilePath(string mediaPath)
        {
            // Versuch 1: Finde eine Datei nach dem Pattern "mediafile.pdf.*.json"
            string mediaFileName = Path.GetFileNameWithoutExtension(mediaPath);
            string mediaDir = Path.GetDirectoryName(mediaPath);
            string[] jsonFiles = Directory.GetFiles(mediaDir, mediaFileName + ".*.json");
            if (jsonFiles.Length > 0)
                return jsonFiles[0];

            // Versuch 2: Finde Dateien die "_COV" vor der Dateiendung haben und suche nach "_CO.json"
            string mediaFileNameWithoutExt = Path.GetFileNameWithoutExtension(mediaFileName);
            string mediaExt = Path.GetExtension(mediaFileName);
            string jsonFileName = mediaFileNameWithoutExt.Replace("_COV", "_CO") + ".json";
            string jsonFilePath = Path.Combine(mediaDir, jsonFileName);
            if (File.Exists(jsonFilePath))
                return jsonFilePath;

            // Versuch 3: Finde Dateien die "_CO" vor der Dateiendung haben und suche nach "_C.json"
            jsonFileName = mediaFileNameWithoutExt.Replace("_CO", "_C") + ".json";
            jsonFilePath = Path.Combine(mediaDir, jsonFileName);
            if (File.Exists(jsonFilePath))
                return jsonFilePath;

            return "";
        }

        /// <summary>
        /// Liest die JSON-Datei und extrahiert einen Unix-Zeitstempel aus den bekannten Feldern.
        /// Es werden die Felder "photoTakenTime" und "creationTime" geprüft.
        /// </summary>
        private static DateTime? ExtractDateFromJson(string jsonFile)
        {
            try
            {
                string jsonContent = File.ReadAllText(jsonFile);
                using (JsonDocument doc = JsonDocument.Parse(jsonContent))
                {
                    JsonElement root = doc.RootElement;
                    long timestamp;
                    if (TryExtractTimestamp(root, "photoTakenTime", out timestamp) ||
                        TryExtractTimestamp(root, "creationTime", out timestamp))
                    {
                        return UnixTimeStampToDateTime(timestamp);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error reading JSON-File " + jsonFile + ": " + ex.Message);
            }
            return null;
        }

        /// <summary>
        /// Versucht, aus dem angegebenen JSON-Knoten den "timestamp" auszulesen.
        /// </summary>
        private static bool TryExtractTimestamp(JsonElement root, string key, out long timestamp)
        {
            timestamp = 0;
            if (root.TryGetProperty(key, out JsonElement element))
            {
                if (element.TryGetProperty("timestamp", out JsonElement tsElement))
                {
                    if (tsElement.ValueKind == JsonValueKind.String)
                    {
                        if (long.TryParse(tsElement.GetString(), out timestamp))
                            return true;
                    }
                    else if (tsElement.ValueKind == JsonValueKind.Number)
                    {
                        if (tsElement.TryGetInt64(out timestamp))
                            return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Wandelt einen Unix-Zeitstempel in ein DateTime um.
        /// </summary>
        private static DateTime UnixTimeStampToDateTime(long unixTimeStamp)
        {
            DateTime dt = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            dt = dt.AddSeconds(unixTimeStamp).ToLocalTime();
            return dt;
        }

        private static bool UpdateImageMetadata(string imagePath, DateTime dateTaken)
        {
            bool success = true;
            try
            {
                using (Image image = Image.FromFile(imagePath))
                {
                    // Verwende ein vorhandenes PropertyItem als Vorlage
                    PropertyItem propItem = image.PropertyItems.Length > 0 ? image.PropertyItems[0] : null;
                    if (propItem == null)
                    {
                        Console.WriteLine("No PropertyItems found in " + imagePath);
                        return false;
                    }

                    // Format: "YYYY:MM:DD HH:MM:SS" (abgeschlossen mit Null-Byte)
                    string dateStr = dateTaken.ToString("yyyy:MM:dd HH:mm:ss\0");
                    byte[] dateBytes = System.Text.Encoding.ASCII.GetBytes(dateStr);
                    // EXIF-Tags: DateTime, DateTimeOriginal, DateTimeDigitized
                    int[] tags = { 0x0132, 0x9003, 0x9004 };

                    foreach (int tag in tags)
                    {
                        propItem.Id = tag;
                        propItem.Type = 2;  // ASCII
                        propItem.Len = dateBytes.Length;
                        propItem.Value = dateBytes;
                        image.SetPropertyItem(propItem);
                    }

                    // Temporäre Datei erstellen, da das Bild gerade verwendet wird
                    string tempFile = Path.GetTempFileName();
                    image.Save(tempFile);

                    // Bild-Objekt freigeben, um die Datei nicht zu sperren
                    image.Dispose();

                    // Originaldatei überschreiben
                    File.Copy(tempFile, imagePath, true);
                    File.Delete(tempFile);

                    Console.WriteLine("Metadata updated for " + imagePath);

                    // Setze zusätzlich die Dateisystem-Daten
                    File.SetCreationTime(imagePath, dateTaken);
                    File.SetLastWriteTime(imagePath, dateTaken);
                    Console.WriteLine("File-Attributes updated for " + imagePath);
                }
            }
            catch (Exception ex)
            {
                success = false;
                Console.WriteLine("Error updating metadata for " + imagePath + ": " + ex.Message);
            }
            return success;
        }

        /// <summary>
        /// Aktualisiert die Dateisystem-Metadaten (Creation- und LastWrite-Time) eines Videos.
        /// </summary>
        private static bool UpdateVideoMetadata(string videoPath, DateTime dateTaken)
        {
            bool success = true;
            try
            {
                File.SetCreationTime(videoPath, dateTaken);
                File.SetLastWriteTime(videoPath, dateTaken);
                Console.WriteLine("File-Attributes updated for " + videoPath);
            }
            catch (Exception ex)
            {
                success = false;
                Console.WriteLine("Error updateing metadata for " + videoPath + ": " + ex.Message);
            }
            return success;
        }
    }
}