// Reads a token through a destructured environment object (planted fault).
const { env } = process;
console.log(env.GH_TOKEN.length);
