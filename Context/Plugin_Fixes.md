# Fixes for Plugin Implementation

## Objective
- Make all of the changes listed within this document. These are almost all plugin related - but not all of them. Be sure to understand and consider all of them. Some changes may require changing the schema of the plugin.json - should that be the case - update and repackage the c:\source\video-sample-plugin and repackage it. 

## General 
1. When Enabling a Plugin, the Plugin features are immediately accessible, when Installing a Plugin, the Plugin features are immediately accessible - this is the expectation -- When Disabling or Uninstalling a Plugin the Plugin features DO NOT immediately go away -- they should immediately go away. 
    - The Application should also handle for cases in which a restart might be required to wire up or uninstall a plugin. If there is ANY REASON that a plugin cannot be immediately enabled/installed OR disabled/uninstalled the user should be notified that a restart is required and should prompt the user to restart the application. If they choose to restart the application should be restarted. 

## Plugins/Extensions Sidebar
1. The highlight around Enabled/Disabled is too large, it should only be slightly bigger than the Text of Enabled/Disabled. It should not resize based on the status, it should be big enough to accomodate both. 
2. When a plugin has not been installed yet Install appears in the Plugin Item in the side panel -- this is correct. When a plugin has been installed Uninstall appears in the plugin item -- lets get rid of this. There should only be a settings cog on the right side there. The user can only uninstall from the main Plugin window

## Main Plugin Tab
1. Enable/Disable should have the same background color as the top menu. The text for Enable should be green. The text for disable should be red.
2. Both the install/uninstall and enable/disable buttons are too large and too far apart from each other. The button size should be smaller (keep the font size) and The install/uninstall and enabled/disable buttons should be closer together. 
3. Because we made the content area tabbed, we no longer need a settings cog anywhere in the plugin tab. Remove it. 
4. Details & Changelog are both coming in in a markdown format. The plugin tabs Details and Changelog sections must render Markdown correctly, not just paste in the contents of the markdown. 
5. The settings tab within the Plugin tab has comboBoxes, and text entry boxes. These must be rounded with the same rounding  as other parts of the application. The must also have the same blue accent highlight as the plugin/extensions sidebar search and combobox and behave the same.
6. Boolean Settings Must be a checkbox. The checkbox should have the same rounding as other places, and the same blue accent highlight, and behave the same. 
7. ComboBoxes must be Dark Modern. 
8. Scroll Bars must have the same style as other scroll bars in the application. 

## Status Bar
1. Change the TopMenu View -> Hide/Show Status Bar to Status Bar and place it underneath Bottom Panel. Add Submenu option to Show/Hide the status Bar. Underneath that, any and all Status Bar objects for plugins should be listed as Show/Hide {Status Bar Object Name} - this Status Bar Object Name should be defined (mandatory) by the Developer. 

## Top Menu button
1. Add a "Vido" Title to the top menu exactly centered. 
2. Plugin buttons should start on the right side of the title
    - There should be a dedicated section for Plugin Buttons. This dedicated section should have the same background color as the main tab color. It should have a border similar to the other borders in the application like the one at the bottom of the top menu. It should be rounded. It should only appear if there is at least 1 plugin installed. The plugin buttons should have the same type of highlight as the other topmenu items. 

## Extra
1. Since the bottom panel is tabbed we should be able to show/hide any and all tabs. 
    - Hide/Show Log Output shoudl be first in the list. 
    - Hide/Show {Bottom Panel Tab Name} should exist for every installed plugin - this Bottom Panel Tab Name should be defined (Mandatory) by the Developer. 