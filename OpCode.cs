namespace zscript;

public enum OpCode : byte
{
    LOADLOCAL,
    LOADCAPTURE,
    LOADCONST,
    LOADSTRING,
    LOADNULL,
    LOADFUNCTION,
    STORELOCAL,
    STORENAME,
    PRINT,
    GETATTR,
    CALL,
    CALLMETHOD,
    AWAIT,
    BINMUL,
    BINDIV,
    BINMOD,
    BINADD,
    BINSUB,
    POPTOP,
    RETURN
}