using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AsseteUsageCheckerTool.Editor
{
    public class AssetUsageChecker
    {
        private static bool _isUsed;
        private static bool _IswarningEnabled;
        private static List<string> _dataFromScene;

        // Determinar si la opción del menú  debe estar habilitada
        [MenuItem("Assets/Verificacion rapida en escenas en build", true)]
        [MenuItem("Assets/Busqueda Rapida en Proyecto", true)]
        [MenuItem("Assets/Verificar Uso", true)]
        private static bool CheckUsageValidation() => Selection.objects.Length == 1;

        [MenuItem("Assets/Verificar Uso")]
        private static void CompleteCheckUsage()
        {
            InitFunctions(out Object selectedObject, out string fileName, out string assetPath, out List<string> folderPaths);

            // Buscar en todas las carpetas del proyecto
            SearchAllFolders(fileName, assetPath, folderPaths);
            // Buscar en todas las escenas en building
            SetDataFromScene(selectedObject);

            FinalDebug(fileName);
        }

        [MenuItem("Assets/Busqueda Rapida en Proyecto")]
        private static void QuickSearchInProject()
        {
            InitFunctions(out _, out string fileName, out string assetPath, out List<string> folderPaths);

            // Buscar en todas las carpetas del proyecto
            SearchAllFolders(fileName, assetPath, folderPaths);

            FinalDebug(fileName);
        }

        [MenuItem("Assets/Verificacion rapida en escenas en build")]
        private static void QuickSearchInBuildingScene()
        {
            InitFunctions(out Object selectedObject, out string fileName, out string assetPath, out List<string> folderPaths);

            SetDataFromScene(selectedObject);

            FinalDebug(fileName);
        }

        private static void InitFunctions(out Object selectedObject, out string fileName, out string assetPath, out List<string> folderPaths)
        {
            selectedObject = Selection.activeObject;

            // Obtener el nombre del archivo seleccionado (sin la extensión)
            fileName = selectedObject.name;

            // Obtener el path del asset seleccionado
            assetPath = AssetDatabase.GetAssetPath(selectedObject);

            // Obtener todas las rutas de los archivos en el proyecto
            string[] allAssetPaths = AssetDatabase.GetAllAssetPaths();
            folderPaths = GetFoldersPaths(allAssetPaths);
            _isUsed = false;
            _IswarningEnabled = false;
        }


        private static void FinalDebug(string fileName)
        {

            if (_IswarningEnabled)
                return;
            // Imprimir las escenas y los GameObjects que utilizan el objeto
            if (_dataFromScene.Count > 0)
            {
                Debug.Log($"El objeto '{fileName}' se utiliza en las siguientes escenas de buildSettings y GameObjects:");
                foreach (string usageInfo in _dataFromScene)
                {
                    Debug.Log(usageInfo);
                }
            }
            else if (_isUsed)
            {
                // si esta en uso en algun componente habria que realizar la busqueda del componente que figura en los logs. Para evitar una baja de rendimiento es preferible que se busque el componente en lugar de que yo vaya loopeandos.
                Debug.Log($"El objeto '{fileName}' puede que no utilize referencias serializables y/o directas visibles en las escenas como para poder mostrar el pathing o componente especifico");
            }

            // Si el asset no está en uso
            if (!_isUsed)
                Debug.Log($"{fileName} no está en uso");

        }

        private static List<string> GetFoldersPaths(string[] allAssetPaths)
        {
            // Filtrar las rutas para quedarse solo con las que son carpetas
            List<string> folderPaths = new List<string>();
            foreach (string currentPath in allAssetPaths)
            {
                if (Directory.Exists(currentPath))
                    folderPaths.Add(currentPath);
            }

            return folderPaths;
        }

        private static void SearchAllFolders(string fileName, string assetPath, List<string> folderPaths)
        {
            foreach (string folderPath in folderPaths)
            {
                // Obtener todos los archivos en la carpeta
                string[] filesInFolder = Directory.GetFiles(folderPath);

                SearchInFolder(fileName, assetPath, filesInFolder);
            }
            Debug.Log("Busqueda en el proyecto finalizada");
        }

        private static void SearchInFolder(string fileName, string assetPath, string[] filesInFolder)
        {
            foreach (string file in filesInFolder)
            {
                // Obtener el path completo del archivo
                string fullPath = file.Replace('\\', '/');

                if (fullPath == assetPath)
                    continue;

                // Si el archivo tiene una referencia al asset seleccionado
                if (AssetDatabase.GetDependencies(fullPath).Contains(assetPath))
                {
                    _isUsed = true;

                    // Obtener el nombre del archivo que está utilizando el asset
                    string usageName = Path.GetFileNameWithoutExtension(fullPath);
                    Debug.Log($"{fileName} está en uso por el archivo {usageName} en {fullPath}");
                }
            }


        }

        private static void SetDataFromScene(Object selectedObject)
        {
            _dataFromScene = new List<string>();

            // Cargar y buscar en todas las escenas del proyecto
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; ++i)
            {
                string scenePath = SceneUtility.GetScenePathByBuildIndex(i);

                SearchInScene(selectedObject, scenePath);
            }
        }

        private static void SearchInScene(Object selectedObject, string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath))
                return;

            bool sceneIsAlreadyOpen = IsSceneAlreadyOpen(scenePath);

            Scene scene = sceneIsAlreadyOpen ? SceneManager.GetSceneByPath(scenePath) : EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            // Buscar todos los GameObjects en la escena que contienen el asset
            GameObject[] allGameObjects = scene.GetRootGameObjects();
            foreach (GameObject go in allGameObjects)
            {
                if (_IswarningEnabled)
                    break;

                SearchThrowGameObject(selectedObject, go, scene.name);
            }

            // Cerrar la escena si no estaba abierta
            if (!sceneIsAlreadyOpen)
                EditorSceneManager.CloseScene(scene, true);
        }

        private static bool IsSceneAlreadyOpen(string scenePath)
        {
            // Verificar si la escena ya está abierta
            bool sceneIsAlreadyOpen = false;

            for (int j = 0; j < SceneManager.sceneCount; j++)
            {
                Scene openScene = SceneManager.GetSceneAt(j);
                if (openScene.path == scenePath)
                {
                    sceneIsAlreadyOpen = true;
                    break;
                }
            }

            return sceneIsAlreadyOpen;
        }

        private static void SearchThrowGameObject(Object selectedAsset, GameObject gameObject, string sceneName)
        {
            if (null == selectedAsset || null == gameObject)
            {
                Debug.LogError("El asset seleccionado es nulo. Por favor, seleccione un asset válido.");
                return;
            }
            try
            {
                SearchGameObject(selectedAsset, gameObject, sceneName);

                SearchForMono(selectedAsset, gameObject, sceneName);

                SearchThrowSerializables(selectedAsset, gameObject, sceneName);

                SearchForChilds(selectedAsset, gameObject, sceneName);

            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Ocurrió un error inesperado al buscar el asset: {ex.Message}");

            }
        }

        private static void SearchGameObject(Object selectedAsset, GameObject gameObject, string sceneName)
        {
            if (selectedAsset is GameObject)
            {
                if (PrefabUtility.GetCorrespondingObjectFromSource(gameObject) == selectedAsset)
                {
                    _dataFromScene.Add($" Nombre de la Escena {sceneName}: Nombre del GameObject '{gameObject.name}'");
                    _isUsed = true;
                }
            }
        }

        private static void SearchForMono(Object selectedAsset, GameObject gameObject, string sceneName)
        {
            if (selectedAsset is MonoScript)
            {
                MonoScript script = selectedAsset as MonoScript;
                Component[] allComponents = gameObject.GetComponents<Component>();
                System.Type scriptType = script.GetClass();

                if (scriptType == null)
                {
                    Debug.LogWarning($"El Script que estas buscando puede que no sea Monobehaviour y/o sea una abstraccion. No se puede buscar en los componentes de la escena.");
                    _IswarningEnabled = true;

                    return;
                }

                foreach (Component component in allComponents)
                {
                    if (component != null && (component.GetType() == scriptType || component.GetType().IsSubclassOf(scriptType)))
                    {
                        _dataFromScene.Add($" Nombre de la Escena {sceneName}: Nombre del GameObject '{gameObject.name}', Nombre del Componente '{component.GetType().Name}'");
                        _isUsed = true;
                    }
                }
            }
        }

        private static void SearchThrowSerializables(Object selectedAsset, GameObject gameObject, string sceneName)
        {
            // Buscar el asset en los componentes 
            Component[] allComponents = gameObject.GetComponents<Component>();
            foreach (Component component in allComponents)
            {
                if (component == null)
                {
                    continue;
                }

                SerializedObject serializedObject = new SerializedObject(component);
                SerializedProperty property = serializedObject.GetIterator();

                while (property.NextVisible(true))
                {
                    if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue != null)
                    {
                        if (PrefabUtility.GetCorrespondingObjectFromSource(property.objectReferenceValue) == selectedAsset || property.objectReferenceValue == selectedAsset)
                        {
                            _dataFromScene.Add($"ObjetoSerializado en : Nombre de la Escena {sceneName}: Nombre del GameObject '{gameObject.name}', Nombre del Componente '{component.GetType().Name}'");
                            _isUsed = true;
                        }
                    }
                }
            }
        }

        private static void SearchForChilds(Object selectedAsset, GameObject gameObject, string sceneName)
        {
            // Buscar el asset en los hijos del GameObject
            foreach (Transform child in gameObject.transform)
            {
                SearchThrowGameObject(selectedAsset, child.gameObject, sceneName);
            }
        }
    }
}
