// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Core.FileNames;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.Projects;

namespace VsChromium.Server.FileSystem {
  public static class ProjectDiscoveryExtensions {
    /// <summary>
    /// Returns the absolute path of the project containing |filename|.
    /// Returns |null| if |filename| is not located within a local project directory.
    /// </summary>
    public static string GetProjectPath(this IProjectDiscovery projectDiscovery, FullPathName filename) {
      var project = projectDiscovery.GetProject(filename);
      if (project == null)
        return null;
      return project.RootPath;
    }

    public static bool IsFileSearchable(this IProjectDiscovery projectDiscovery, FileName filename) {
      var project = projectDiscovery.GetProjectFromRootPath(new FullPathName(filename.GetProjectRoot().Name));
      if (project == null)
        return false;
      return project.SearchableFilesFilter.Include(filename.GetRelativePathFromProjectRoot());
    }
  }
}
