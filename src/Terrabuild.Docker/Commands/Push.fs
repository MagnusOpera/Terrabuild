namespace Terrabuild.Docker.Push
open Terrabuild.Extensibility
open Helpers


type Arguments = {
    NodeHash: string
    Image: string
}


type Command(context: Context) =
    interface ICommandBuilder<Arguments> with
        member _.GetSteps parameters =
            if context.CI then
                let retagArgs = $"buildx imagetools create -t {parameters.Image}:$(terrabuild_branch_or_tag) {parameters.Image}:{parameters.NodeHash}"
                [ buildCmdLine "docker" retagArgs Cacheability.Remote ]
            else
                let tagArgs = $"tag {parameters.Image}:{parameters.NodeHash} {parameters.Image}:$(terrabuild_branch_or_tag)"
                [ buildCmdLine "docker" tagArgs Cacheability.Local ]
