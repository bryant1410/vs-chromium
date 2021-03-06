﻿// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VsChromium.Core.Configuration;
using VsChromium.Core.Files;
using VsChromium.Core.Linq;
using VsChromium.Core.Logging;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.FileSystemSnapshot;
using VsChromium.Server.Projects;

namespace VsChromium.Server.FileSystem {
  public class FileSystemChangesValidator {
    private readonly IFileSystemNameFactory _fileSystemNameFactory;
    private readonly IFileSystem _fileSystem;
    private readonly IProjectDiscovery _projectDiscovery;

    public FileSystemChangesValidator(
      IFileSystemNameFactory fileSystemNameFactory,
      IFileSystem fileSystem,
      IProjectDiscovery projectDiscovery) {
      _fileSystemNameFactory = fileSystemNameFactory;
      _fileSystem = fileSystem;
      _projectDiscovery = projectDiscovery;
    }

    public FileSystemValidationResult ProcessPathsChangedEvent(IList<PathChangeEntry> changes) {
      // Skip files from filtered out directories
      var filteredChanges = changes
        .Where(x => !PathIsExcluded(x))
        .ToList();

      if (Logger.Info) {
        Logger.LogInfo("ProcessPathsChangedEvent: {0:n0} items left out of {1:n0} after filtering (showing max 5 below).",
          filteredChanges.Count, changes.Count);
        filteredChanges
          .Take(5)
          .ForAll(x =>
            Logger.LogInfo("  Path changed: \"{0}\", kind={1}", x.Path, x.Kind));
      }

      if (filteredChanges.Count == 0) {
        Logger.LogInfo("All changes have been filtered out.");

        return new FileSystemValidationResult {
          NoChanges = true,
        };
      }

      if (filteredChanges.Any(x => IsProjectFileChange(x))) {
        Logger.LogInfo("At least one change is a project file.");

        return new FileSystemValidationResult {
          UnknownChanges = true,
        };
      }

      if (filteredChanges.All(x => x.Kind == PathChangeKind.Changed)) {
        Logger.LogInfo("All file change events are file modifications.");

        var fileNames = filteredChanges
          .Select(change => GetProjectFileName(change.Path))
          .Where(name => !name.IsNull);

        return new FileSystemValidationResult {
          FileModificationsOnly = true,
          ModifiedFiles = fileNames.ToList()
        };
      }

      // All kinds of file changes
      Logger.LogInfo("Some file change events are create or delete events.");
      return new FileSystemValidationResult {
        VariousFileChanges = true,
        FileChanges = new FullPathChanges(filteredChanges)
      };
    }

    private static bool IsProjectFileChange(PathChangeEntry change) {
      return 
        SystemPathComparer.Instance.StringComparer.Equals(change.Path.FileName, ConfigurationFileNames.ProjectFileNameObsolete) ||
        SystemPathComparer.Instance.StringComparer.Equals(change.Path.FileName, ConfigurationFileNames.ProjectFileName);
    }

    private bool PathIsExcluded(PathChangeEntry change) {
      var path = change.Path;
      var project = _projectDiscovery.GetProject(path);
      if (project == null)
        return true;

      // If path is root itself, it is never excluded.
      if (path == project.RootPath)
        return false;

      // Split relative part into list of name components.
      var split = PathHelpers.SplitPrefix(path.Value, project.RootPath.Value);
      var relativePath = split.Suffix;
      var names = relativePath.Split(Path.DirectorySeparatorChar);

      // Check each relative path from root path to full path.
      var pathToItem = new RelativePath();
      foreach (var item in names) {
        var relativePathToItem = pathToItem.CreateChild(item);

        bool exclude;
        // For the last component, we might not if it is a file or directory.
        // Check depending on the change kind.
        if (item == names.Last()) {
          if (change.Kind == PathChangeKind.Deleted) {
            exclude = false;
          } else {
            var info = _fileSystem.GetFileInfoSnapshot(path);
            if (info.IsFile) {
              exclude = !project.FileFilter.Include(relativePathToItem);
            } else if (info.IsDirectory) {
              exclude = !project.DirectoryFilter.Include(relativePathToItem);
            } else {
              // We don't know... Be conservative.
              exclude = false;
            }
          }
        } else {
          exclude = !project.DirectoryFilter.Include(relativePathToItem);
        }

        if (exclude)
          return true;

        pathToItem = relativePathToItem;
      }
      return false;
    }

    private ProjectFileName GetProjectFileName(FullPath path) {
      return FileSystemNameFactoryExtensions.GetProjectFileName(_fileSystemNameFactory, _projectDiscovery, path);
    }
  }
}
