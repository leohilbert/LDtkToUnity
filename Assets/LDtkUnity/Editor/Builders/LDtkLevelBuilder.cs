﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace LDtkUnity.Editor.Builders
{
    public class LDtkLevelBuilder
    {
        private readonly LDtkProjectImporter _importer;
        private readonly LdtkJson _json;
        private readonly Level _level;
        
        private GameObject _levelGameObject;
        private GameObject _layerGameObject;
        private Grid _layerGrid;




        private LDtkSortingOrder _sortingOrder;
        private LDtkBuilderTileset _builderTileset;
        private LDtkBuilderIntGridValue _builderIntGrid;
        private LDtkBuilderEntity _entityBuilder;
        private LDtkLevelBackgroundBuilder _backgroundBuilder;


        public LDtkLevelBuilder(LDtkProjectImporter importer, LdtkJson json, Level level)
        {
            _importer = importer;
            _json = json;
            _level = level;
        }


        
        /// <summary>
        /// Returns the root of the object hierarchy of the layers
        /// </summary>
        public GameObject BuildLevel()
        {
            if (!CanTryBuildLevel())
            {
                return null;
            }
            
            InvokeWithinTimer(BuildLayerInstances);

            return _levelGameObject.gameObject;
        }
        
        private void InvokeWithinTimer(Action action)
        {
            Stopwatch levelBuildTimer = Stopwatch.StartNew();
            action.Invoke();
            levelBuildTimer.Stop();

            if (!_importer.LogBuildTimes)
            {
                return;
            }
            
            double ms = levelBuildTimer.ElapsedMilliseconds;
            Debug.Log($"LDtk: Built level \"{_level.Identifier}\" in {ms}ms ({ms/1000}s)");
        }

        private bool CanTryBuildLevel()
        {
            if (_importer == null)
            {
                Debug.LogError("LDtk: ProjectAssets object is null; not building level.");
                return false;
            }

            if (_json == null)
            {
                Debug.LogError("LDtk: project data null; not building level.");
                return false;
            }

            if (_level == null)
            {
                Debug.LogError("LDtk: level null; not building level.");
                return false;
            }

            if (!DoesLevelsContainLevel(_json.Levels, _level))
            {
                Debug.LogError("LDtk: level not contained within these levels in the project; not building level.");
                return false;
            }

            return true;
        }

        private bool DoesLevelsContainLevel(Level[] levels, Level levelToBuild)
        {
            if (levelToBuild == null)
            {
                Debug.LogError($"LDtk: LevelToBuild null, not assigned?");
                return false;
            }
            
            if (levels.Any(lvl => string.Equals(lvl.Identifier, levelToBuild.Identifier)))
            {
                return true;
            }
            
            Debug.LogError($"LDtk: No level named \"{levelToBuild}\" exists in the LDtk Project");
            return false;
        }


        
        private void BuildLayerInstances()
        {
            _levelGameObject = InstantiateLevelRootObject();
            _levelGameObject.transform.position = _level.UnityWorldSpaceCoord(_importer.PixelsPerUnit);
            
            if (_importer.DeparentInRuntime)
            {
                _levelGameObject.AddComponent<LDtkDetachChildren>();
            }
            
            _sortingOrder = new LDtkSortingOrder();
            
            //build layers and background from front to back in terms of ordering 
            foreach (LayerInstance layer in _level.LayerInstances)
            {
                BuildLayerInstance(layer);
            }
            
            _backgroundBuilder = new LDtkLevelBackgroundBuilder(_importer, _layerGameObject, _sortingOrder, _level);
            _backgroundBuilder.BuildBackground();
            

        }

        private GameObject InstantiateLevelRootObject()
        {
            if (_json.Defs.LevelFields.IsNullOrEmpty())
            {
                return DefaultObject();
            }
            
            if (_importer.LevelFieldsPrefab != null)
            {
                return GetFieldInjectedLevelObject();
            }
                
            Debug.LogWarning("The LDtk project has level fields defined, but there is no scripted level prefab assigned.");
            return DefaultObject();

            GameObject DefaultObject()
            {
                return new GameObject(_level.Identifier);
            }
            
            GameObject GetFieldInjectedLevelObject()
            {
                GameObject obj = Object.Instantiate(_importer.LevelFieldsPrefab);
                obj.name = _level.Identifier;
            
                LDtkFieldInjector fieldInjector = new LDtkFieldInjector(obj, _level.FieldInstances);
                fieldInjector.InjectEntityFields();
                return obj;
            }
        }
        
        private void BuildLayerInstance(LayerInstance layer)
        {
            _layerGameObject = _levelGameObject.AddChild(layer.Identifier);


            
            //entities layer is different from the other three types
            if (layer.IsEntitiesLayer)
            {
                _entityBuilder = new LDtkBuilderEntity(_importer, _layerGameObject, _sortingOrder);
                _entityBuilder.SetLayer(layer);
                _entityBuilder.BuildEntityLayerInstances();
                return;
            }

            _layerGrid = _layerGameObject.AddComponent<Grid>();

            _builderTileset = new LDtkBuilderTileset(_importer, _layerGameObject, _sortingOrder);
            
            if (layer.IsTilesLayer)
            {
                _builderTileset.SetLayer(layer);
                _builderTileset.BuildTileset(layer.GridTiles);
            }
            
            if (layer.IsIntGridLayer)
            {
                _builderIntGrid = new LDtkBuilderIntGridValue(_importer, _layerGameObject, _sortingOrder);
                _builderIntGrid.SetLayer(layer);
                _builderIntGrid.BuildIntGridValues();
            }
            
            //an int grid layer could also be an auto layer
            if (layer.IsAutoLayer)
            {
                _builderTileset.SetLayer(layer);
                _builderTileset.BuildTileset(layer.AutoLayerTiles);
            }
        }
    }
}