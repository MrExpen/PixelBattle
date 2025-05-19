namespace PixelBattle.Binary;

public interface IBinarySerializable<out T> : IBinaryLength, IBinaryReadable<T>, IBinaryWritable
    where T : struct;