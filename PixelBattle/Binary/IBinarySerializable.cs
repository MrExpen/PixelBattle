namespace PixelBattle.Binary;

public interface IBinarySerializable<T> : IBinaryLength, IBinaryReadable<T>, IBinaryWritable
    where T : struct, allows ref struct;