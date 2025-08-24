using UnityEngine;
using System.Threading.Tasks;

namespace InventoryCounter
{
    public class InventoryCounterMod : IMod
    {
        private InventoryCounterManager? _manager;
        private bool _isRetrying = false;

        public void OnLoad()
        {
            Debug.Log("[InventoryCounter] OnLoad");
            TryAttachToGameManager();
        }

        private void TryAttachToGameManager()
        {
            GameObject? gmObj = null;
            if (GameManager.gameManagerInstance != null)
            {
                gmObj = GameManager.gameManagerInstance.gameObject;
            }
            if (gmObj == null)
            {
                gmObj = GameObject.Find("GameManager");
            }

            if (gmObj == null)
            {
                if (!_isRetrying)
                {
                    _isRetrying = true;
                    DelayedRetry();
                }
                return;
            }

            // Create manager if missing
            if (_manager == null)
            {
                var go = new GameObject("InventoryCounterManager");
                _manager = go.AddComponent<InventoryCounterManager>();
                _manager.Initialize();
            }

            _isRetrying = false;
        }

        private async void DelayedRetry()
        {
            await Task.Delay(1000);
            _isRetrying = false;
            TryAttachToGameManager();
        }

        public void OnUnload()
        {
            if (_manager != null)
            {
                Object.Destroy(_manager.gameObject);
                _manager = null;
            }
        }

        public void OnUpdate()
        {
            if (_manager == null && !_isRetrying)
            {
                TryAttachToGameManager();
            }
        }
    }
}

