v1.0.6
  - Some small optimizations
  - Fix addons being deselected after material swapping/scene updates/etc
  - Fix possible name conflict issues with addons
  - Fix off-by-one for selected avatar name matching when drag&dropping an avatar
  - Add version to the tool window's title

v1.0.5
  - Switch from forced file existance to initialized lock file
  - Code formatting
  - Add "Custom (Drag&Drop below)" to the avatar selection dropdown
  - Add ability to offset the addon's installation path
  - Add logging for warnings/errors
  - Fix AssetDatabase refreshing after default binding config copy
  - Fix custom avatar handling
  - Fix offset parent handling for addon prefabs
  - Fix possible index-out-of-bounds errors when there is a different number of materials between the definition and avatar
  - Fix issue where processing stops when an addon already exists
  - Move addon handling into it's own method

v1.0.4
  - Fix asset importing after creation

v1.0.3
  - Fix silly issue with VPM deleting non-default definitions on update

v1.0.2
  - Add GPL-3.0 header to code file
  - Fix awkward template file name and exclude it from the UI

v1.0.1
  - Fix VPM incompatible Assets path

v1.0.0
  - Initial release
