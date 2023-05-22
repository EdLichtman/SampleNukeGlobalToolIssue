# SampleNukeGlobalToolIssue
The sample repository for demonstrating the Nuke Global Tool issue

Basically, from the root of the repository, run "nuke".

When you run the target with expected behavior of "Working", then all we're doing to make it work is adding "nuke.common" project reference to the global tool. When we set it to "Not Working", we're removing the project reference.

I would imagine that if an internal library has reference to nuke.common, and another library accesses it, the final library accessing it is going to be a nuke tool.