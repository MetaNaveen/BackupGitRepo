**DESCRIPTION:**

Tool to help developers who use git. This tool takes backup of all files that are changed since last commit in the project.

**SUPPORTING COMMANDS:**

    BackupGitRepo.exe [<OPTIONS>] <REPODIR> [<BACKUPDIR>]

    <OPTIONS>:
    -su = skips untracked files
    -ii = includes git ignored files/folders

    <REPODIR>: Directory path to the git project. 
    Use dot '.' to refer current directory from where the tool is used.

    <BACKUPDIR>: Backup folder path. 
    If not provided <REPODIR> is considered.
    Creates folder like Backup_<DirNameOrDriveLetter>_<yyyyMMMdd>_<incrementalInteger> inside the <BACKUPDIR> provided (eg: Backup_GitProject1_2024Sep16_1).  Use dot '.' to refer current directory from where the tool is used.

**SAMPLE USAGE:**

    BackupGitRepo -su -ii . .
    BackupGitRepo -su C:\Projects\GitProject1 C:\Backups\
    BackupGitRepo . C:\Backups\