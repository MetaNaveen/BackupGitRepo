**DESCRIPTION:**

A Self updatable single file tool that helps developers to take backup of git repo files that are changed since last commit in the project.

**SUPPORTING COMMANDS:**

    BackupGitRepo.exe [<OPTIONS>] <REPODIR> [<BACKUPDIR>]

    <OPTIONS>:
    -su = skips untracked files
    -ii = includes git ignored files/folders

    <REPODIR>: Directory path to the git project. 
    Use dot '.' to refer current directory from where the tool is used.

    <BACKUPDIR>: Backup folder path. 
    If not provided <REPODIR> is considered.
    Creates folder like Backup_<DirNameOrDriveLetter>_<yyyyMMdd>_<incrementalInteger> inside the <BACKUPDIR> provided (eg: Backup_GitProject1_20241016_1).  Use dot '.' to refer current directory from where the tool is used.

**SAMPLE USAGE:**

    BackupGitRepo -su -ii . .
    BackupGitRepo -su C:\Projects\GitProject1 C:\Backups\
    BackupGitRepo . C:\Backups\