using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OneNoteMarkdownExporter.Services
{
    public sealed class ExportConfigFile
    {
        public int Version { get; set; } = 1;
        public string OutputPath { get; set; } = ExportOptions.GetDefaultOutputPath();
        public string? AssetsFolderPath { get; set; }
        public AssetOrganizationMode AssetOrganizationMode { get; set; } = AssetOrganizationMode.Centralized;
        public bool Overwrite { get; set; }
        public bool ApplyLinting { get; set; } = true;
        public string? LintConfigPath { get; set; }
        public bool ExportAll { get; set; }
        public List<string>? NotebookNames { get; set; }
        public List<string>? SectionPaths { get; set; }
        public List<string>? PageIds { get; set; }
        public bool DryRun { get; set; }
        public bool Verbose { get; set; }
        public bool Quiet { get; set; }
        public bool PreserveDates { get; set; } = true;
        public DateMetadataMode DateMetadataMode { get; set; } = DateMetadataMode.None;
        public bool ListMode { get; set; }

        public ExportOptions ToExportOptions()
        {
            return new ExportOptions
            {
                OutputPath = string.IsNullOrWhiteSpace(OutputPath) ? ExportOptions.GetDefaultOutputPath() : OutputPath,
                AssetsFolderPath = AssetsFolderPath,
                AssetOrganizationMode = AssetOrganizationMode,
                Overwrite = Overwrite,
                ApplyLinting = ApplyLinting,
                LintConfigPath = LintConfigPath,
                ExportAll = ExportAll,
                NotebookNames = NotebookNames,
                SectionPaths = SectionPaths,
                PageIds = PageIds,
                DryRun = DryRun,
                Verbose = Verbose,
                Quiet = Quiet,
                PreserveDates = PreserveDates,
                DateMetadataMode = DateMetadataMode
            };
        }

        public static ExportConfigFile FromExportOptions(ExportOptions options, bool listMode)
        {
            return new ExportConfigFile
            {
                OutputPath = options.OutputPath,
                AssetsFolderPath = options.AssetsFolderPath,
                AssetOrganizationMode = options.AssetOrganizationMode,
                Overwrite = options.Overwrite,
                ApplyLinting = options.ApplyLinting,
                LintConfigPath = options.LintConfigPath,
                ExportAll = options.ExportAll,
                NotebookNames = options.NotebookNames,
                SectionPaths = options.SectionPaths,
                PageIds = options.PageIds,
                DryRun = options.DryRun,
                Verbose = options.Verbose,
                Quiet = options.Quiet,
                PreserveDates = options.PreserveDates,
                DateMetadataMode = options.DateMetadataMode,
                ListMode = listMode
            };
        }
    }

    public static class ExportConfigFileService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        public static string GetDefaultConfigPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OneNoteMarkdownExporter",
                "export-config.json");
        }

        public static ExportConfigFile Load(string? path = null)
        {
            var effectivePath = string.IsNullOrWhiteSpace(path) ? GetDefaultConfigPath() : Path.GetFullPath(path);
            if (!File.Exists(effectivePath))
            {
                return new ExportConfigFile();
            }

            var json = File.ReadAllText(effectivePath);
            var config = JsonSerializer.Deserialize<ExportConfigFile>(json, JsonOptions);
            return config ?? new ExportConfigFile();
        }

        public static bool TryLoad(string? path, out ExportConfigFile? config, out string? error)
        {
            config = null;
            error = null;

            var effectivePath = string.IsNullOrWhiteSpace(path) ? GetDefaultConfigPath() : Path.GetFullPath(path);
            if (!File.Exists(effectivePath))
            {
                error = $"Config file not found: {effectivePath}";
                return false;
            }

            try
            {
                var json = File.ReadAllText(effectivePath);
                config = JsonSerializer.Deserialize<ExportConfigFile>(json, JsonOptions);
                if (config == null)
                {
                    error = $"Config file is empty or invalid: {effectivePath}";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = $"Could not load config '{effectivePath}': {ex.Message}";
                return false;
            }
        }

        public static void Save(ExportConfigFile config, string? path = null)
        {
            var effectivePath = string.IsNullOrWhiteSpace(path) ? GetDefaultConfigPath() : Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(effectivePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(effectivePath, json);
        }
    }
}
