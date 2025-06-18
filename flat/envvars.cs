#:property DefineConstants=$(DefineConstants);CUSTOM_CONSTANT;$(EnvVarConstant)

#if DEBUG
Console.WriteLine("Hello from Debug!");
#endif

#if RELEASE
Console.WriteLine("Hello from Release!");
#endif

#if CUSTOM_CONSTANT
Console.WriteLine("CUSTOM_CONSTANT is enabled!");
#endif

#if ENV_VAR_CONSTANT
Console.WriteLine("EnvVarConstant is enabled!");
#endif
