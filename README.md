# Google-Photos-Takout-Converter
This tool processes media files exported from **Google Photos Takeout** and restores their metadata, such as the correct creation date, which Google stores separately in JSON files. The tool ensures that both **images and videos** have their original timestamps properly set.

## Features
- Reads **Google Photos Takeout** folders.
- Extracts metadata from corresponding `.json` files.
- Updates **EXIF metadata** for images.
- Sets correct timestamps for **videos**.
- Processes multiple folders automatically and recursively.

## Usage
### Download the Binary (Windows)
1. Download the latest release from the [Releases](https://github.com/harryneufeld/Google-Photos-Takout-Converter/releases) page.
2. Run the exe
3. When prompted, **enter the takeout-directory** where your (extracted) Google Takeout export is located. Make sure you merged all multiple Takeout folders into one big.
6. The tool will scan all subfolders and process images (`.jpg`, `.png`) and videos (`.mp4`, `.mov`, `.avi`, `.mkv`).
7. After processing, your media files will have the correct timestamps. Failed attempts or unsupported mediatypes will leave the metadata-files and the media files untouched.

## Installation (For Developers)
1. **Clone the repository**:
   ```sh
   git clone https://github.com/harryneufeld/Google-Photos-Takout-Converter.git
   cd Google-Photos-Takout-Converter
   ```
2. **Build the project** (if necessary):
   ```sh
   dotnet build
   ```
3. **Run the program**:
   ```sh
   dotnet run
   ```

## License
This project is licensed under the **MIT License with Commons Clause**, meaning commercial usage is restricted without explicit permission. See the LICENCE file for details.

## Contributions
Contributions are welcome! Please open an issue or submit a pull request if you’d like to improve the tool.

## Disclaimer
This software is provided "as is," without warranty of any kind. Use at your own risk. Back up your files before using this tool. If things go wrong, your files may be corrupted.

---
**Author:** Harry Neufeld  
**Repository:** [GitHub](https://github.com/harryneufeld/Google-Photos-Takout-Converter)

