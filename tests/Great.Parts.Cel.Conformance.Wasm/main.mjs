// Starts the runtime for WebAssembly under Node.js and runs Main of the runner; the exit code of Main is the exit code of the process.
import { dotnet } from './wwwroot/_framework/dotnet.js';

const { runMain } = await dotnet
    .withApplicationArguments(...process.argv.slice(2))
    .create();

const exitCode = await runMain();
process.exit(exitCode);
