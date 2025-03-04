namespace SourceControls
open Environment

type Local() =
    let commitLog = currentDir() |> Git.getCommitLog
    let commit = commitLog.Head

    interface Contracts.ISourceControl with
        override _.BranchOrTag = currentDir() |> Git.getBranchOrTag
        
        override _.HeadCommit =
            { Sha = commit.Sha
              Message = commit.Subject
              Author = commit.Author
              Email = commit.Email
              Timestamp = commit.Timestamp }
        
        override _.CommitLog =
            commitLog.Tail
            |> List.map (fun commit -> 
                { Sha = commit.Sha
                  Message = commit.Subject
                  Author = commit.Author
                  Email = commit.Email
                  Timestamp = commit.Timestamp })

        override _.Run = None

        override _.LogType = Contracts.LogType.Terminal
        override _.LogError _ = ()
