using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Serialization;
using System.Text.Json;

namespace MetadataProcessor
{
    public static class MediaProcessor
    {
        // Unterstützte Dateiendungen
        private static readonly string[] ImageExtensions = { ".arw", ".gif", ".png", ".jpg", ".jpeg", ".raw" };
        private static readonly string[] VideoExtensions = { ".mp4", ".mov", ".avi", ".mkv" };

        /// <summary>
        /// Durchsucht den Root-Ordner rekursiv nach JSON-Dateien, parst sie und verarbeitet die darin angegebenen Mediendateien.
        /// </summary>
        public static void ProcessRootFolder(string rootFolder)
        {
            var jsonFiles = Directory.EnumerateFiles(rootFolder, "*.json", SearchOption.AllDirectories);
            foreach (string jsonFile in jsonFiles)
            {
                ProcessJsonFile(jsonFile);
            }
        }

        private static void ProcessJsonFile(string jsonFile)
        {
            string mediaFileName = ExtractMediaFilePathFromJson(jsonFile);
            string mediaFilePath = Path.Combine(Path.GetDirectoryName(jsonFile), mediaFileName);
            if (string.IsNullOrEmpty(mediaFilePath) || !File.Exists(mediaFilePath))
            {
                Console.WriteLine("No valid media file found in JSON-File: " + jsonFile);
                return;
            } else
            {
                Console.WriteLine($"Processing file {mediaFilePath}");
            }

                DateTime? dateTaken = ExtractDateFromJson(jsonFile);
            if (dateTaken.HasValue)
            {
                if (IsImageFile(mediaFilePath))
                {
                    if (UpdateImageMetadata(mediaFilePath, dateTaken.Value))
                    {
                        // Wenn die .MP-Datei existiert, auch deren Metadaten aktualisieren
                        string mpFilePath = GetMPFilePath(mediaFilePath);
                        if (!string.IsNullOrEmpty(mpFilePath) && File.Exists(mpFilePath))
                        {
                            if (UpdateMPMetadata(mpFilePath, dateTaken.Value))
                            {
                                //Console.WriteLine("Metadata updated for MP file: " + mpFilePath);
                            }
                        }

                        // JSON-Datei löschen, nachdem die Metadaten erfolgreich aktualisiert wurden
                        File.Delete(jsonFile);
                    }
                }
                else if (IsVideoFile(mediaFilePath))
                {
                    if (UpdateVideoMetadata(mediaFilePath, dateTaken.Value))
                        File.Delete(jsonFile);
                }
            }
            else
            {
                Console.WriteLine("No date found in JSON-File for " + mediaFilePath);
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

        private static string ExtractMediaFilePathFromJson(string jsonFile)
        {
            try
            {
                string jsonContent = File.ReadAllText(jsonFile);
                using (JsonDocument doc = JsonDocument.Parse(jsonContent))
                {
                    JsonElement root = doc.RootElement;
                    if (root.TryGetProperty("title", out JsonElement titleElement))
                    {
                        return titleElement.GetString();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error reading JSON-File " + jsonFile + ": " + ex.Message);
            }
            return null;
        }

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
                        // Wenn keine PropertyItems vorhanden sind, erstellen wir neue PropertyItems für die Zeitstempel
                        if (propItem == null)
                        {
                            propItem = (PropertyItem)FormatterServices.GetUninitializedObject(typeof(PropertyItem));
                        }

                        // Format: "YYYY:MM:DD HH:MM:SS" (abgeschlossen mit Null-Byte)
                        string dateStrNew = dateTaken.ToString("yyyy:MM:dd HH:mm:ss\0");
                        byte[] dateBytesNew = System.Text.Encoding.ASCII.GetBytes(dateStrNew);
                        // EXIF-Tags: DateTime, DateTimeOriginal, DateTimeDigitized
                        int[] tagsNew = { 0x0132, 0x9003, 0x9004 };

                        foreach (int tag in tagsNew)
                        {
                            propItem.Id = tag;
                            propItem.Type = 2;  // ASCII
                            propItem.Len = dateBytesNew.Length;
                            propItem.Value = dateBytesNew;
                            image.SetPropertyItem(propItem);
                        }
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

                    //Console.WriteLine("Metadata updated for " + imagePath);

                    // Setze zusätzlich die Dateisystem-Daten
                    File.SetCreationTime(imagePath, dateTaken);
                    File.SetLastWriteTime(imagePath, dateTaken);
                    //Console.WriteLine("File-Attributes updated for " + imagePath);
                }
            }
            catch (Exception ex)
            {
                success = false;
                Console.WriteLine("Error updating metadata for " + imagePath + ": " + ex.Message);
            }
            return success;
        }

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

        private static bool UpdateMPMetadata(string mpFilePath, DateTime dateTaken)
        {
            bool success = true;
            try
            {
                // Da keine EXIF-Daten vorhanden sind, gehen wir davon aus, dass die .MP-Datei einfach die Dateiattribute benötigt.
                // Setze die Dateisystem-Metadaten
                File.SetCreationTime(mpFilePath, dateTaken);
                File.SetLastWriteTime(mpFilePath, dateTaken);
                //Console.WriteLine("File-Attributes updated for MP file: " + mpFilePath);
            }
            catch (Exception ex)
            {
                success = false;
                Console.WriteLine("Error updating metadata for MP file " + mpFilePath + ": " + ex.Message);
            }
            return success;
        }

        private static bool UpdateVideoMetadata(string videoPath, DateTime dateTaken)
        {
            bool success = true;
            try
            {
                File.SetCreationTime(videoPath, dateTaken);
                File.SetLastWriteTime(videoPath, dateTaken);
                //Console.WriteLine("File-Attributes updated for " + videoPath);
            }
            catch (Exception ex)
            {
                success = false;
                Console.WriteLine("Error updating metadata for " + videoPath + ": " + ex.Message);
            }
            return success;
        }
    }
}