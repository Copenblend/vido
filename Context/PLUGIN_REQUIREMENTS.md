## Side Panel Plugin Requirements
### Every element must be in line with the existing application. Dark Modern Blue accents etc. 
1. A Search Bar that searches on title and tags
2. A Drop down (Dark Modern) That lists all the names of all of the available plugin registries + and "All" that will show all the plugins across all of the repositories - This should be the same width as the Search Bar 
3. 2 Collapsible panels (top down collapse just like VS code) - The number of plugins for each section should be part of the Panel title. A Gray circle with a white number inside. The collapsing should be uniform with the rest of the app using the Chevrons. For these pointing right is closed, and pointing down is open.
    - Installed (Top panel)
    - Available

4. The plugins side Panel MUST accurate replicate the VS Code Plugins Side Panel in form and function
    - The Items in the side panel must have:
        - Icon for the plugin Left Justified
        - Title for the plugin in Bold White to the right of the icon
        - Short Description of the plugin that should be a lighter color and normal non bold font, like a light grey (just like VS Code) beneath the title
            - Slightly smaller font than the title same styling as the short description
        - Publisher of the plugin beneath the description
            - Smaller font than the short description same lighter 
            - If the plugin is from the official Vido plugin registry
                - The Publisher should be listed as Vido
                - There should be a blue (Accent Blue) circle with a thin black checkmark to signify it is official.
        - If the Plugin appears in the **Installed** Panel
            - Settings Cog for managing settings -- This should be on the right side aligned with the Publisher
        - If the Plugin appears in the **Available** Panel
            - Install Button
                - Small (Blue Accent just like VS Code) with white font -- This should be on the right side aligned with the publisher 

## Main Window Requirements
- This should look just like it does in VS Code for the most part. 
    - Header
        - Large Icon at the top Left
        - Title to the right of the Icon in bold white
        - Publisher (same rules as the side panel)
        - Short Description (same rules as the side panel)
        - Under short description two buttons in line with each other with no color
            - Install/Unistall
                - MUST REPRESENT ACCURATE STATE - i.e. if installed should show Uninstall and vice versa
            - Enable/Disable
                - MUST REPRESENT ACCURATE STATE - i.e. if enabled should show Disable and vice versa
        - Settings Button to the right of the two buttons
    - Content 
        - Split into two sections left and right. Both independently scrollable
        - Left section 75%
            - Tabbed
                - Make the tabs identical to the VS Code Plugin Tabs That have Details | Features | Changelog | Dependencies - only we will only have Details and Changelog and Settings
                    - DETAILS
                        - README.md document from the plugin displayed in Markdown Format
                    - CHANGELOG
                        - CHANGELOG.md document from the plugin displayed in Markdown Format
                    - SETTINGS
                        - All of the settings that the user can change that are relative only to this plugin
        - Right Section 25%
            - Version
            - Tags
            - Last Updated
            - License

## Settings Requirements
- The settings for a plugin should be the same experience no matter where the user selects a settings cog for the plugin
    - It open to the setting tab of the plugin in the main panel
    - Developers have 4 options for settings that they can allow users to set
        - Checkbox - true/false boolean values
        - ComboBox - multiple choice, may only select 1, developer must provide the available options
        - String - User entered text
        - Number - User entered value - entry is restricted to numbers only
    - Settings overwrite requirements
        - The default for all plugins should be that the settings values set by the user are kept
        - The developer must specify defaults for all settings
            - None is an option
        - The developer MAY choose to force override user settings if they choose, for a breaking change for example
    - Additional Requirements
        - The developer MAY specify sections within their settings
            - Sections should be separate with a thin divider like other places in the application
            - Sections should have a title or header
