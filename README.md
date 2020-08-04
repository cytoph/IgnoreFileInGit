# Ignore Files in Git

Know the struggle of having .config or similar files in your repo which are used as a template, but then should be updated with your own local settings without being checked in again?

Then this extension is for you!

It works by using the git command `git update-index --skip-worktree` which basically let's you ignore a file that's already been tracked, so it'll never be checked in again accidentally.
For further information visit https://git-scm.com/docs/git-update-index#Documentation/git-update-index.txt---no-skip-worktree.

## Features

Open your solution and wait until it's fully loaded. Then right click any file in the solution explorer and you'll see the entry 'Ignore File in Git'.
Click it and the file will no longer be tracked. This will also be indicated by a checkbox in front of the context menu entry when you right click the file again.

Similarly, you can right click a file and select 'Ignore File in Git' to continue tracking it again.