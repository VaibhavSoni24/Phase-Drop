using PhaseDrop.Core.Models;

namespace PhaseDrop.Core.Registry;

public static class CapabilityRegistry
{
    private static readonly Dictionary<string, OperationCategory> ExtensionCategoryMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Images
        { ".png", OperationCategory.Image },
        { ".jpg", OperationCategory.Image },
        { ".jpeg", OperationCategory.Image },
        { ".webp", OperationCategory.Image },
        { ".bmp", OperationCategory.Image },
        { ".gif", OperationCategory.Image },
        { ".tiff", OperationCategory.Image },
        { ".tif", OperationCategory.Image },
        { ".ico", OperationCategory.Image },

        // Video
        { ".mp4", OperationCategory.Video },
        { ".mov", OperationCategory.Video },
        { ".mkv", OperationCategory.Video },
        { ".webm", OperationCategory.Video },
        { ".avi", OperationCategory.Video },
        { ".wmv", OperationCategory.Video },
        { ".flv", OperationCategory.Video },
        { ".m4v", OperationCategory.Video },

        // Audio
        { ".mp3", OperationCategory.Audio },
        { ".wav", OperationCategory.Audio },
        { ".flac", OperationCategory.Audio },
        { ".aac", OperationCategory.Audio },
        { ".m4a", OperationCategory.Audio },
        { ".ogg", OperationCategory.Audio },
        { ".opus", OperationCategory.Audio },
        { ".wma", OperationCategory.Audio },

        // PDF
        { ".pdf", OperationCategory.Pdf },

        // Archives
        { ".zip", OperationCategory.Archive },
        { ".tar", OperationCategory.Archive },
        { ".gz", OperationCategory.Archive },
        { ".tgz", OperationCategory.Archive },
        { ".7z", OperationCategory.Archive },
        { ".rar", OperationCategory.Archive },

        // Documents & Data
        { ".csv", OperationCategory.Document },
        { ".tsv", OperationCategory.Document },
        { ".txt", OperationCategory.Document },
        { ".json", OperationCategory.Document },
        { ".html", OperationCategory.Document },
        { ".htm", OperationCategory.Document },
        { ".md", OperationCategory.Document }
    };

    public static OperationCategory GetCategory(string path)
    {
        if (Directory.Exists(path))
            return OperationCategory.Archive;

        string ext = Path.GetExtension(path);
        return ExtensionCategoryMap.TryGetValue(ext, out var category) ? category : OperationCategory.Unknown;
    }

    public static OperationCategory GetCategory(IEnumerable<string> paths)
    {
        var categories = paths.Select(GetCategory).Distinct().ToList();
        if (categories.Count == 1)
            return categories[0];
        
        // If mixed or directory present, treat as archive packing candidates
        return OperationCategory.Archive;
    }

    public static IReadOnlyList<FileOperation> GetConversions(IEnumerable<string> paths)
    {
        var category = GetCategory(paths);
        return category switch
        {
            OperationCategory.Image => [
                new("convert:png", "PNG", ".png", OperationCategory.Image, false, "ImagePng", "High fidelity lossless raster image", true),
                new("convert:jpg", "JPEG", ".jpg", OperationCategory.Image, false, "ImageJpg", "High compatibility compressed photo", false),
                new("convert:webp", "WebP", ".webp", OperationCategory.Image, false, "ImageWebp", "Modern high efficiency web image", false),
                new("convert:gif", "GIF", ".gif", OperationCategory.Image, false, "ImageGif", "Animated or indexed palette image", false),
                new("convert:bmp", "BMP", ".bmp", OperationCategory.Image, false, "ImageBmp", "Uncompressed Windows bitmap", true),
                new("convert:tiff", "TIFF", ".tiff", OperationCategory.Image, false, "ImageTiff", "Archival tagged image format", true),
                new("convert:ico", "ICO", ".ico", OperationCategory.Image, false, "ImageIco", "Windows application and icon format", true),
                new("convert:pdf", "PDF", ".pdf", OperationCategory.Image, false, "DocumentPdf", "Encapsulated PDF document", true)
            ],
            OperationCategory.Video => [
                new("convert:mp4", "MP4", ".mp4", OperationCategory.Video, false, "VideoMp4", "H.264 / AAC standard video", false),
                new("convert:webm", "WebM", ".webm", OperationCategory.Video, false, "VideoWebm", "VP9 / Opus open web video", false),
                new("convert:mkv", "MKV", ".mkv", OperationCategory.Video, false, "VideoMkv", "Matroska multimedia container", true),
                new("convert:mov", "MOV", ".mov", OperationCategory.Video, false, "VideoMov", "QuickTime video format", false),
                new("convert:avi", "AVI", ".avi", OperationCategory.Video, false, "VideoAvi", "Audio Video Interleave format", false),
                new("convert:gif", "GIF", ".gif", OperationCategory.Video, false, "VideoGif", "Animated GIF loop", false),
                new("convert:mp3", "MP3", ".mp3", OperationCategory.Video, false, "AudioMp3", "Extract audio to MP3", false),
                new("convert:wav", "WAV", ".wav", OperationCategory.Video, false, "AudioWav", "Extract audio to lossless WAV", true)
            ],
            OperationCategory.Audio => [
                new("convert:mp3", "MP3", ".mp3", OperationCategory.Audio, false, "AudioMp3", "Universal compressed audio", false),
                new("convert:wav", "WAV", ".wav", OperationCategory.Audio, false, "AudioWav", "Lossless PCM audio", true),
                new("convert:flac", "FLAC", ".flac", OperationCategory.Audio, false, "AudioFlac", "Free Lossless Audio Codec", true),
                new("convert:aac", "AAC", ".m4a", OperationCategory.Audio, false, "AudioAac", "Advanced Audio Coding (M4A)", false),
                new("convert:ogg", "OGG", ".ogg", OperationCategory.Audio, false, "AudioOgg", "Ogg Vorbis audio container", false),
                new("convert:opus", "Opus", ".opus", OperationCategory.Audio, false, "AudioOpus", "State of the art Opus audio", false)
            ],
            OperationCategory.Pdf => [
                new("convert:zip", "ZIP", ".zip", OperationCategory.Archive, false, "ArchiveZip", "Compress PDF into ZIP archive", true),
                new("convert:7z", "7-Zip", ".7z", OperationCategory.Archive, false, "Archive7z", "Compress PDF into 7z archive", true)
            ],
            OperationCategory.Archive => [
                new("convert:zip", "ZIP", ".zip", OperationCategory.Archive, false, "ArchiveZip", "Standard cross-platform ZIP archive", true),
                new("convert:7z", "7-Zip", ".7z", OperationCategory.Archive, false, "Archive7z", "High-compression LZMA 7z archive", true),
                new("convert:tar", "TAR", ".tar", OperationCategory.Archive, false, "ArchiveTar", "Unix tape archive container", true),
                new("convert:tgz", "TAR.GZ", ".tar.gz", OperationCategory.Archive, false, "ArchiveTgz", "Compressed tarball archive", true)
            ],
            OperationCategory.Document => [
                new("convert:csv", "CSV", ".csv", OperationCategory.Document, false, "DocCsv", "Comma separated tabular data", true),
                new("convert:json", "JSON", ".json", OperationCategory.Document, false, "DocJson", "JavaScript Object Notation", true),
                new("convert:html", "HTML", ".html", OperationCategory.Document, false, "DocHtml", "HTML table or formatted document", true),
                new("convert:txt", "TXT", ".txt", OperationCategory.Document, false, "DocTxt", "Plain text extraction", true)
            ],
            _ => [
                new("convert:zip", "ZIP", ".zip", OperationCategory.Archive, false, "ArchiveZip", "Compress into ZIP archive", true),
                new("convert:7z", "7-Zip", ".7z", OperationCategory.Archive, false, "Archive7z", "Compress into 7-Zip archive", true)
            ]
        };
    }

    public static IReadOnlyList<FileOperation> GetTools(IEnumerable<string> paths)
    {
        var category = GetCategory(paths);
        return category switch
        {
            OperationCategory.Image => [
                new("tool:crop", "Crop", ".png", OperationCategory.Image, true, "ToolCrop", "Interactive crop with visual frame", false, true),
                new("tool:resize", "Resize", ".png", OperationCategory.Image, true, "ToolResize", "Resize by dimensions or scale percentage", false, true),
                new("tool:compress", "Compress", ".jpg", OperationCategory.Image, true, "ToolCompress", "Optimize and compress image size", false, true),
                new("tool:rotate_cw", "Rotate 90°", ".png", OperationCategory.Image, true, "ToolRotate", "Rotate 90 degrees clockwise", true, false),
                new("tool:flip_h", "Flip H", ".png", OperationCategory.Image, true, "ToolFlip", "Flip horizontally", true, false),
                new("tool:strip_exif", "Strip EXIF", ".png", OperationCategory.Image, true, "ToolShield", "Remove privacy metadata & GPS tags", true, false),
                new("tool:img_to_pdf", "To PDF", ".pdf", OperationCategory.Image, true, "DocumentPdf", "Assemble image(s) into PDF document", true, false)
            ],
            OperationCategory.Video => [
                new("tool:trim", "Trim", ".mp4", OperationCategory.Video, true, "ToolTrim", "Trim start and end timestamps", true, true),
                new("tool:compress", "Compress", ".mp4", OperationCategory.Video, true, "ToolCompress", "Re-encode with custom target bitrate", false, true),
                new("tool:extract_audio", "Audio Track", ".mp3", OperationCategory.Video, true, "AudioMp3", "Extract audio track from video", false, false),
                new("tool:to_gif", "Make GIF", ".gif", OperationCategory.Video, true, "VideoGif", "Generate animated GIF clip", false, true),
                new("tool:speed", "Speed", ".mp4", OperationCategory.Video, true, "ToolSpeed", "Speed up or slow down video (0.5x - 2.0x)", false, true),
                new("tool:thumbnail", "Snapshot", ".png", OperationCategory.Video, true, "ToolCamera", "Extract still frame snapshot", true, true)
            ],
            OperationCategory.Audio => [
                new("tool:trim", "Trim", ".mp3", OperationCategory.Audio, true, "ToolTrim", "Trim audio start/end duration", true, true),
                new("tool:compress", "Compress", ".mp3", OperationCategory.Audio, true, "ToolCompress", "Adjust bitrate and channel configuration", false, true),
                new("tool:normalize", "Normalize", ".mp3", OperationCategory.Audio, true, "ToolVolume", "EBU R128 loudness normalization", false, false)
            ],
            OperationCategory.Pdf => [
                new("tool:merge_pdf", "Merge PDFs", ".pdf", OperationCategory.Pdf, true, "ToolMerge", "Combine multiple PDFs in sequence", true, false),
                new("tool:split_pdf", "Split All", ".pdf", OperationCategory.Pdf, true, "ToolSplit", "Split PDF into individual pages", true, false),
                new("tool:extract_pages", "Extract", ".pdf", OperationCategory.Pdf, true, "ToolExtract", "Extract specific page range", true, true),
                new("tool:rotate_pages", "Rotate Pages", ".pdf", OperationCategory.Pdf, true, "ToolRotate", "Rotate pages 90° or 180°", true, true),
                new("tool:strip_metadata", "Clean Info", ".pdf", OperationCategory.Pdf, true, "ToolShield", "Strip author, title, and PDF metadata", true, false)
            ],
            OperationCategory.Archive => [
                new("tool:extract", "Extract Here", "", OperationCategory.Archive, true, "ToolExtract", "Extract archive into dedicated folder", true, false),
                new("tool:pack_zip", "Pack ZIP", ".zip", OperationCategory.Archive, true, "ArchiveZip", "Compress into ZIP archive", true, false),
                new("tool:pack_7z", "Pack 7-Zip", ".7z", OperationCategory.Archive, true, "Archive7z", "Compress into 7z archive", true, false),
                new("tool:test_archive", "Verify", "", OperationCategory.Archive, true, "ToolCheck", "Test archive integrity and checksums", true, false)
            ],
            OperationCategory.Document => [
                new("tool:table_to_json", "To JSON", ".json", OperationCategory.Document, true, "DocJson", "Convert CSV/TSV table to JSON objects", true, false),
                new("tool:md_to_html", "MD to HTML", ".html", OperationCategory.Document, true, "DocHtml", "Render Markdown document to HTML", true, false)
            ],
            _ => [
                new("tool:pack_zip", "Pack ZIP", ".zip", OperationCategory.Archive, true, "ArchiveZip", "Compress selection to ZIP", true, false),
                new("tool:pack_7z", "Pack 7-Zip", ".7z", OperationCategory.Archive, true, "Archive7z", "Compress selection to 7z", true, false)
            ]
        };
    }

    public static FileOperation? FindOperation(string operationId, IEnumerable<string> paths)
    {
        var all = GetConversions(paths).Concat(GetTools(paths));
        return all.FirstOrDefault(op => string.Equals(op.Id, operationId, StringComparison.OrdinalIgnoreCase));
    }
}
