using UnityEngine;

public interface ISaveDataProvider<T>
{
    T CaptureSaveData();
    void RestoreSaveData(T data);
}

public class SaveManager : MonoBehaviour
{
    
}
