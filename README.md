# VRChat Avatar Material Swapper

This tool was made by the Torinyan team to help people swap their avatar materials quickly in-editor.  
It also allows creators to specify some addon prefabs that'll be placed as a child-object to the selected avatar.

## End-User How-To

1. Open the tool via `Tools` > `Torinyan` > `Material Swapper`  
  ![How To Open The Tool](.docs/HowTo_Open_Tool.png)
2. Select your avatar, either via the select box, or the drag&drop input
3. Select any addons you want to add to your avatar, if there are any
4. Click the button for the material you wish to swap to  
  ![Example Tool Window](.docs/HowTo_Window_Example.png)
5. Done

## Material Creator How-To

1. Duplicate (`Ctrl`+`D` in Unity Editor) the template file  
  You can find the file here: `Packages/[Torinyan] Material Swapper/Resources/Template.json`  
  For the Torinyan avatar BAN you can duplicate `BAN_Default.json` instead
2. Rename the new file to whatever you like
3. Edit the new file in any text editor, even Notepad will do (Double-click the file in Unity Editor to open it)  
  Be sure to follow JSON formatting standards. You can test your file using a JSON-validator, like:  
  https://jsonlint.com/
4. Always test your work, better safe than sorry ^_^
5. Be sure you have the material definition file selected during your export  
  `Ctrl`+`Left Click` allows you to select multiple folders and files  
  ![Export Example](.docs/HowTo_Export_Example.png)
6. Be sure to notify your clientele that they can use this tool to swap their materials in seconds!
