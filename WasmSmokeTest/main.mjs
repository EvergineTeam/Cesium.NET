import { dotnet } from './_framework/dotnet.js'

const { runMain } = await dotnet.create();
const exitCode = await runMain();

// Hand the managed exit code to the shell, so a failed assertion fails the job rather than
// printing FAIL into a green run.
process.exit(exitCode);
