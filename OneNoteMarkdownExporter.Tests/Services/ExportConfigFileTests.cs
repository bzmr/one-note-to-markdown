using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using OneNoteMarkdownExporter.Services;
using Xunit;

namespace OneNoteMarkdownExporter.Tests.Services;

public class ExportConfigFileTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsAllSupportedFields()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"onenote-export-config-{Guid.NewGuid():N}.json");

        try
        {
            var config = new ExportConfigFile
            {
                Version = 1,
                OutputPath = @"C:\\Export",
                AssetsFolderPath = @"C:\\ExportAssets",
                AssetOrganizationMode = AssetOrganizationMode.Section,
                Overwrite = true,
                ApplyLinting = false,
                LintConfigPath = @"C:\\lint.json",
                ExportAll = false,
                NotebookNames = new List<string> { "NotebookA" },
                SectionPaths = new List<string> { "NotebookA/SectionA" },
                PageIds = new List<string> { "page-1" },
                DryRun = true,
                Verbose = true,
                Quiet = false,
                PreserveDates = false,
                DateMetadataMode = DateMetadataMode.Yaml,
                ListMode = true
            };

            ExportConfigFileService.Save(config, tempFile);
            var loaded = ExportConfigFileService.Load(tempFile);

            loaded.OutputPath.Should().Be(config.OutputPath);
            loaded.AssetsFolderPath.Should().Be(config.AssetsFolderPath);
            loaded.AssetOrganizationMode.Should().Be(config.AssetOrganizationMode);
            loaded.Overwrite.Should().Be(config.Overwrite);
            loaded.ApplyLinting.Should().Be(config.ApplyLinting);
            loaded.LintConfigPath.Should().Be(config.LintConfigPath);
            loaded.ExportAll.Should().Be(config.ExportAll);
            loaded.NotebookNames.Should().BeEquivalentTo(config.NotebookNames);
            loaded.SectionPaths.Should().BeEquivalentTo(config.SectionPaths);
            loaded.PageIds.Should().BeEquivalentTo(config.PageIds);
            loaded.DryRun.Should().Be(config.DryRun);
            loaded.Verbose.Should().Be(config.Verbose);
            loaded.Quiet.Should().Be(config.Quiet);
            loaded.PreserveDates.Should().Be(config.PreserveDates);
            loaded.DateMetadataMode.Should().Be(config.DateMetadataMode);
            loaded.ListMode.Should().Be(config.ListMode);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void ToExportOptions_MapsConfigValues()
    {
        var config = new ExportConfigFile
        {
            OutputPath = @"C:\\Export",
            AssetsFolderPath = @"C:\\ExportAssets",
            AssetOrganizationMode = AssetOrganizationMode.Page,
            Overwrite = true,
            ApplyLinting = false,
            LintConfigPath = @"C:\\lint.json",
            ExportAll = true,
            NotebookNames = new List<string> { "N" },
            SectionPaths = new List<string> { "N/S" },
            PageIds = new List<string> { "p" },
            DryRun = true,
            Verbose = true,
            Quiet = true,
            PreserveDates = false,
            DateMetadataMode = DateMetadataMode.Yaml
        };

        var options = config.ToExportOptions();

        options.OutputPath.Should().Be(config.OutputPath);
        options.AssetsFolderPath.Should().Be(config.AssetsFolderPath);
        options.AssetOrganizationMode.Should().Be(config.AssetOrganizationMode);
        options.Overwrite.Should().Be(config.Overwrite);
        options.ApplyLinting.Should().Be(config.ApplyLinting);
        options.LintConfigPath.Should().Be(config.LintConfigPath);
        options.ExportAll.Should().Be(config.ExportAll);
        options.NotebookNames.Should().BeEquivalentTo(config.NotebookNames);
        options.SectionPaths.Should().BeEquivalentTo(config.SectionPaths);
        options.PageIds.Should().BeEquivalentTo(config.PageIds);
        options.DryRun.Should().Be(config.DryRun);
        options.Verbose.Should().Be(config.Verbose);
        options.Quiet.Should().Be(config.Quiet);
        options.PreserveDates.Should().Be(config.PreserveDates);
        options.DateMetadataMode.Should().Be(config.DateMetadataMode);
    }
}
