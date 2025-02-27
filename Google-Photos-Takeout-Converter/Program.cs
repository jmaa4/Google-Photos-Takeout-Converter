using MetadataProcessor;

namespace Google_Photos_Takout_Converter
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Please enter takout-directory path (full path):");
            string rootFolder = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(rootFolder) || !Directory.Exists(rootFolder))
            {
                Console.WriteLine("This directory does not exist.");
                return;
            }

            MediaProcessor.ProcessRootFolder(rootFolder);

            Console.WriteLine("Processing finished. Press enter to close.");
            Console.ReadKey();
        }
    }
}
