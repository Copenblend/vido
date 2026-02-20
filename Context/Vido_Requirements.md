# Base Functionality
1. Vido is to be an ultra-performant video player. The goal is to make it the most performant video player on the market. Whenever there is a conflict performance should weigh more heavily. The application should start up immediately, it should resize and move smoothly, it should seem like the most professionally put together video player on the market. 

2. Vido must support all of the most common fileTypes such as mp4, avi, etc. Specialized codecs can be added as plugins later. 

3. Video must have an clean, minimal UI by default. The UI MUST be identical to VS Code (Dark Modern) but geared towards playing videos. 
- It should have the same style top menu, side menu, open panels, status bar
- It should support Tabs, for example the video player would be a tab.
- It should support tabs on the right side or the bottom and they should be draggable to dock them in other places. Docking must work seemlessly. 
- It must have basic video player functionality in the video player. 
    - Play/Pause
    - Stop
    - Skip Forward
    - Skip Backward
    - Volume
    - Loop
- One of the options in the left hand should be a file explorer that looks identical to VS Code
- Another one of the options in the left hand should be a plugins explorer/manager just like VS Code.
    - The user must be able to view plugins install/uninstall and manage their settings. 
- Another one of the initial options is the settings
    - This should open in a tab the same way as it does in VS Code
- The File explorer must support context menu
    - If the user clicks in the general area (not on a file) the context menu should allow a user to either close the folder, open a folder, or rescan the current folder for new files. 
- The File explorer must support icons for video files to start, and every other file must be a generic icon. 
    - This must be easily updatable via plugins. 
- The middle tab, which ever tab is open should always fill the space left when the left, right, or bottom sections are minimized. 
- Everything must have good design, ENSURE YOU USE AS MUCH OF VSCODE as is open source and usable, icons controls, etc. 

4. Vido must be ultra extensible. It should be extremely easy for developers to add additional functionality. This means the solution must be ultra modular. This must be built into the design. Examples of extensibility: 
- Add new icons for different types of files
- Add new icons to the left tab
- Create new draggable tabs that could apply to either the right side tabs or bottom tabs. The initial base program will not have any right side or bottom tabs BUT MUST support them. 
    - An example of a right side tab is Copilot Chat
    - And example of a bottom tab would be the terminal 
- Create new Top Menu buttons
    - An example of this is the Chat button for Copilot chat. 
- They should NOT be able to add to the Top Menus main menu (File/Edit/Help, etc.)
- Change what is displayed in the Status bar on the bottom. 
- Users must be able to search for extensions by name. 
- Plugins must be auto-updatable, or update only when desired. 

5. Initial Tabs
- The initial right side tab should show details about the video being played. It should show all of the meta data in a nice readable format. 
- The intial bottom tab should be a console that logs events - these logs should be understandable for the average user once complete, but during development it can be used to output information about whats happening like click actions. 
- All tabs should be collapsable and dismissable. The top menu View button should support reshowing or hiding the right or bottom tabs

# Requirements

- Create a full, comprehensive and exhaustive implementation document for the above features. It must leave nothing to chance. The developer AI (Claude Opus 4.6) must understand exactly what they must do, what the requirements are, etc. 

- The AI must be instructed to ensure the code is super clean. Any time they make a change they MUST look over the code and ensure they have left no dead code. If it makes sense to move something to make it more readable to humans they should do that as well. It should look like a human wrote it but have the performance of an expert AI with super human coding capabilities. 

- The AI must setup the solution to start, including all the pieces that need to be in place for a human reviewer to start the application and manually test it. That should be step one of the implementation plan. 

- The AI MUST ensure the solution is super modular according to the base functionality requirements. I will be adding a TCode plugin soon after the original application is done, and I expect it to be very easily pluggable. I want the plugin functionality added in early so that we can test adding plugins. 

- The Implementation plan must implement the entire plugin system - OR - instruct the human reader what they need to do to hook up and connect the plugins to the system. For example if there is some cloud repository for hosting plugins the implementation plan must specifiy exactly how to do that. (Ideally the plugin system is super easy, maybe something like a json list on github that can list plugins hosted from other repositories on github) 

- Part of the implementation plan must be to details exactly what the Plugin API is so that either a human or an AI can easily integrate with Vido. There should be no ambiguity there. 

- The Implementation plan must instruct the AI to - AFTER EVERY TICKET - List the Changelog and provide a git commit message. 

- The implementation plan MUST be broken down like tickets for an agile team - each ticket should provide some new visibile and testable functionality. 
    - Regular feature Ticket should be in the format vi-xxx where xs are the numbers. For example vido-001
    - Bug or fix tickets should be in the format vi-b-xxx where xxx are the number and b signifies we are fixing something

- The implementation plan must instruct the AI to create a new markdown document (PER TICKET) with the manual and regression tests that should be performed to ensure that everything works correctly. - These should be placed in a TEST_PLANS folder within the Context folder. 

- The implementation plan MUST be completely free, using open source things. 

- The imlementation plan MUST end with creating a portable zip of the application with the lightest weight possible. 