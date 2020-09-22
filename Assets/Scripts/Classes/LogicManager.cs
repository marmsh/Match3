﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Match3Project.Classes.Commands;
using Match3Project.Classes.StaticClasses;
using Match3Project.Enums;
using Match3Project.Interfaces;
using Match3Project.Interfaces.Cells;
using Match3Project.Interfaces.Command;
using UnityEngine;

namespace Match3Project.Classes
{
    public class LogicManager : MonoBehaviour, ILogicManager
    {
        private IBoard _board;
        private ICheckManager _checkManager;
        private ISpawnManager _spawnManager;

        private Vector3 _clickA;
        private Vector3 _clickB;

        private ICommand _macroCommand;

        private GameStates _gameState;

        private bool _isMatchedSwipe;
        private int _swipeCounter;

        private IDictionary<GameObject, Vector2> _fallCellsDictionary;

        private IDictionary<Vector2, PowerUpTypes> _powersDictionary;
        private IDictionary<Vector3, PowerUpTypes> _spawnedPowerUpDictionary;

        private IDictionary<ICell, IList<ICell>> _matchedCellsDictionary;

        private IList<Color> _colorsList;

        private ICell _lastFallCell;
        private bool _lastSpawnedCell;

        private bool _reverseGravity;
        private int _spawnYPos;
        private bool _hasPowerUps;

        private void Awake()
        {
            _fallCellsDictionary = new Dictionary<GameObject, Vector2>();

            _powersDictionary = new Dictionary<Vector2, PowerUpTypes>();
            _spawnedPowerUpDictionary = new Dictionary<Vector3, PowerUpTypes>();

            _matchedCellsDictionary = new Dictionary<ICell, IList<ICell>>();

            _colorsList = new List<Color>();

            _gameState = GameStates.Ready;
            _reverseGravity = false;
            _hasPowerUps = false;
        }

        public void OnEvent(EventTypes eventType, object messageData)
        {
            switch (eventType)
            {
                case EventTypes.LMB_Down:
                    _clickA = (Vector3) messageData;
                    break;

                case EventTypes.LMB_Up:
                    _clickB = (Vector3) messageData;

                    if (_gameState == GameStates.Ready)
                    {
                        if (_clickA.x >= 0 && _clickA.x < _board.Width && _clickA.y >= 0 && _clickA.y < _board.Height &&
                            (Mathf.Abs(_clickB.x - _clickA.x) > StringsAndConst.SWIPE_SENSITIVITY ||
                             Mathf.Abs(_clickB.y - _clickA.y) > StringsAndConst.SWIPE_SENSITIVITY))
                        {
                            MoveDirectionTypes swipeDirection = Helper.FindMoveDirection(_clickA, _clickB);
                            SwipeCells(swipeDirection);
                        }
                    }

                    break;

                case EventTypes.Swipe:
                    _gameState = GameStates.Wait;
                    ExecuteMacroCommand();
                    break;

                case EventTypes.CELL_EndMove:
                    TryCheckSwipedCells((ICell) messageData);
                    break;

                case EventTypes.CELL_EndMoveBack:
                    ICell cellBack = (ICell) messageData;

                    _board.Cells[cellBack.TargetX, cellBack.TargetY] = cellBack;
                    cellBack.CellState = CellStates.Wait;

                    _gameState = GameStates.Ready;
                    break;

                case EventTypes.CELL_Fall:
                    ICell cellFall = (ICell) messageData;

                    cellFall.CellState = CellStates.Wait;

                    if (cellFall == _lastFallCell)
                    {
                        CheckBoard();
                    }

                    break;

                case EventTypes.POWER_Use:
                    ArrayList arr = (ArrayList) messageData;

                    PowerUpTypes powerUp = Helper.StringToPowerType(arr[0].ToString());
                    Vector3 position = (Vector3) arr[1];

                    _powersDictionary.Add(position, powerUp);

                    break;

                case EventTypes.BOARD_Collapse:
                    ExecuteMacroCommand();
                    break;

                case EventTypes.BOARD_EndDestroyMatchedCells:
                    if (_powersDictionary.Count > 0)
                        _hasPowerUps = true;

                    if (_hasPowerUps)
                    {
                        foreach (var power in _powersDictionary)
                        {
                            Vector2 pos = power.Key;
                            PowerUpTypes powerUpType = power.Value;

                            if (powerUpType == PowerUpTypes.Gravity)
                            {
                                _reverseGravity = !_reverseGravity;
                            }

                            List<ICell> cellsList = new List<ICell>(_checkManager.PowerCheck(powerUpType, pos));
                            ICell cell = _board.Cells[(int) pos.x, (int) pos.y];

                            if (_matchedCellsDictionary.ContainsKey(cell) == false)
                            {
                                _matchedCellsDictionary.Add(cell, cellsList);
                            }
                        }

                        _powersDictionary.Clear();
                        _hasPowerUps = false;

                        StartCoroutine(DestroyMatchedCells(_matchedCellsDictionary));
                    }
                    else
                    {
                        StartCoroutine(RefillBoard());
                    }

                    break;

                default:
                    Debug.Log(StringsAndConst.EVENT_NOT_FOUND);
                    break;
            }
        }

        private void TryCheckSwipedCells(ICell cell)
        {
            _swipeCounter++;

            IList<ICell> cellsList = new List<ICell>(_checkManager.CheckCell(cell));

            if (cellsList.Count > 2 || cell.CurrentGameObject.CompareTag(StringsAndConst.TAG_POWER))
            {
                if (cell.CurrentGameObject.CompareTag(StringsAndConst.TAG_POWER))
                {
                    cellsList.Add(cell);
                }

                _matchedCellsDictionary.Add(cell, cellsList);

                _isMatchedSwipe = true;
            }

            if (_swipeCounter > 1)
            {
                if (_isMatchedSwipe)
                {
                    StartCoroutine(DestroyMatchedCells(_matchedCellsDictionary));
                }
                else
                {
                    _matchedCellsDictionary.Clear();

                    UndoMacroCommand();
                }

                _isMatchedSwipe = false;
                _swipeCounter = 0;
            }
        }

        private void CheckBoard()
        {
            _lastSpawnedCell = false;

            _fallCellsDictionary.Clear();

            if (Helper.HaveMatches(_checkManager))
            {
                FindMatches();
                return;
            }

            _gameState = GameStates.Ready;
        }

        public void FindMatches()
        {
            foreach (var cell in _board.Cells)
            {
                AxisTypes majorAxis = AxisTypes.Undefined;

                IList<ICell> matchedCellsList = new List<ICell>();

                if (Helper.CellIsEmpty(cell) == false)
                {
                    if (cell.CellState != CellStates.Check)
                    {
                        matchedCellsList = _checkManager.CheckCell(cell);
                        _matchedCellsDictionary.Add(cell, matchedCellsList);
                    }
                }
            }

            StartCoroutine(DestroyMatchedCells(_matchedCellsDictionary));
        }

        private IEnumerator DestroyMatchedCells(IDictionary<ICell, IList<ICell>> matchedCellsDictionary)
        {
            foreach (var cellList in matchedCellsDictionary)
            {
                if (cellList.Key.CurrentGameObject != null)
                {
                    if (cellList.Value.Count > 3)
                    {
                        int matchCount = cellList.Value.Count;

                        _colorsList.Add(Helper.DetectColor(cellList.Key));

                        PowerUpTypes powerUp = Helper.DetectPowerUp(matchCount);
                        _spawnedPowerUpDictionary.Add(
                            new Vector3(cellList.Key.TargetX, cellList.Key.TargetY, 0f), powerUp);
                    }
                }

                WorkAfterMatch(cellList.Value);
            }

            _matchedCellsDictionary.Clear();

            yield return new WaitForSeconds(StringsAndConst.TIME_AFTER_DESTROY);

            OnEvent(EventTypes.BOARD_EndDestroyMatchedCells, null);
        }
        
        private void WorkAfterMatch(IList<ICell> cellsAfterMarkList)
        {
            foreach (var cell in cellsAfterMarkList)
            {
                cell.DoAfterMatch();
            }
        }

        private IEnumerator RefillBoard()
        {
            if (_spawnedPowerUpDictionary.Count > 0)
            {
                int i = 0;
                foreach (var spawnedPowerUp in _spawnedPowerUpDictionary)
                {
                    GameObject spawnedPowerUpGO =
                        _spawnManager.SpawnPowerPrefab(spawnedPowerUp.Value, spawnedPowerUp.Key);

                    Helper.SetGravityPowerUpColor(spawnedPowerUpGO, _colorsList[i]);

                    _board.Cells[(int) spawnedPowerUp.Key.x, (int) spawnedPowerUp.Key.y].CurrentGameObject =
                        spawnedPowerUpGO;

                    i++;
                }
            }

            _spawnedPowerUpDictionary.Clear();
            _colorsList.Clear();

            DecreaseBoard();

            yield return new WaitForSeconds(StringsAndConst.TIME_AFTER_DECREASE);

            SpawnNewCells();
        }

        private void SpawnNewCells()
        {
            IList<Vector2> spawnTarget = new List<Vector2>();

            for (int j = 0; j < _board.Height; j++)
            {
                for (int i = 0; i < _board.Width; i++)
                {
                    if (_board.Cells[i, j].CurrentGameObject == null)
                    {
                        spawnTarget.Add(new Vector2(i, j));
                    }
                }
            }

            StartCoroutine(FindTargetForNewCell(spawnTarget));
        }

        private IEnumerator FindTargetForNewCell(IList<Vector2> spawnTargets)
        {
            IDictionary<int, IList<Vector2>> spawnTargetsDictionary = new Dictionary<int, IList<Vector2>>();

            if (_reverseGravity)
            {
                _spawnYPos = -1;

                for (int i = _board.Height; i >= 0; i--)
                {
                    List<Vector2> tempList = new List<Vector2>();

                    foreach (var spawnTarget in spawnTargets)
                    {
                        if (spawnTarget.y == i)
                        {
                            tempList.Add(spawnTarget);
                        }
                    }

                    spawnTargetsDictionary.Add(i, tempList);
                }
            }
            else
            {
                _spawnYPos = _board.Height;
                for (int i = 0; i < _board.Height; i++)
                {
                    List<Vector2> tempList = new List<Vector2>();

                    foreach (var spawnTarget in spawnTargets)
                    {
                        if (spawnTarget.y == i)
                        {
                            tempList.Add(spawnTarget);
                        }
                    }

                    spawnTargetsDictionary.Add(i, tempList);
                }
            }


            foreach (var row in spawnTargetsDictionary)
            {
                if (row.Key == spawnTargetsDictionary.Keys.Last())
                {
                    _lastSpawnedCell = true;
                }

                if (row.Value.Count > 0)
                {
                    SpawnRow(row.Value);
                    yield return new WaitForSeconds(StringsAndConst.TIME_BETWEEN_SPAWN);
                }
            }
        }

        private void SpawnRow(IList<Vector2> positionsList)
        {
            IList<ICell> cells = new List<ICell>();

            foreach (var position in positionsList)
            {
                Vector3 tempPosition = new Vector3(position.x, _spawnYPos, 0f);
                GameObject spawnedGameObject = _spawnManager.SpawnPrefab(tempPosition);

                ICell tempCell = _board.Cells[(int) position.x, (int) position.y];
                tempCell.CurrentGameObject = spawnedGameObject;

                cells.Add(tempCell);
            }

            if (_lastSpawnedCell)
            {
                _lastFallCell = cells.Last();
            }

            StartFallCommand(cells);
        }

        private void DecreaseBoard()
        {
            if (_reverseGravity)
            {
                for (int i = _board.Width - 1; i >= 0; i--)
                {
                    for (int j = _board.Height - 1; j >= 0; j--)
                    {
                        if (_board.Cells[i, j].CurrentGameObject == null)
                        {
                            for (int k = j - 1; k >= 0; k--)
                            {
                                if (_board.Cells[i, k].CurrentGameObject != null)
                                {
                                    SetNewFallTarget(i, j, k);
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                for (int i = 0; i < _board.Width; i++)
                {
                    for (int j = 0; j < _board.Height; j++)
                    {
                        if (_board.Cells[i, j].CurrentGameObject == null)
                        {
                            for (int k = j + 1; k < _board.Height; k++)
                            {
                                if (_board.Cells[i, k].CurrentGameObject != null)
                                {
                                    SetNewFallTarget(i, j, k);
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            StartFall(_fallCellsDictionary);
        }

        private void SetNewFallTarget(int i, int j, int k)
        {
            GameObject go = _board.Cells[i, k].CurrentGameObject;
            _fallCellsDictionary.Add(go, new Vector2(i, j));

            _board.Cells[i, k].CurrentGameObject = null;
        }

        private void StartFall(IDictionary<GameObject, Vector2> dictionary)
        {
            IList<ICell> cells = new List<ICell>();

            foreach (var cell in dictionary)
            {
                ICell tempCell = _board.Cells[(int) cell.Value.x, (int) cell.Value.y];
                tempCell.CurrentGameObject = cell.Key;

                cells.Add(tempCell);
            }

            StartFallCommand(cells);
        }

        private void StartFallCommand(IList<ICell> cellsList)
        {
            ICommand[] commands = new ICommand[cellsList.Count];

            for (int i = 0; i < cellsList.Count; i++)
            {
                commands[i] = new FallCommand(cellsList[i]);
            }

            SetMacroCommand(commands);
            OnEvent(EventTypes.BOARD_Collapse, null);
        }
        
        private void SwipeCells(MoveDirectionTypes direction)
        {
            int xPos = (int) Mathf.Round(_clickA.x);
            int yPos = (int) Mathf.Round(_clickA.y);

            ICell cellA = _board.Cells[xPos, yPos];

            if (cellA.CurrentGameObject != null)
            {
                switch (direction)
                {
                    case MoveDirectionTypes.Up:
                        if (yPos < _board.Height - 1)
                        {
                            ICell cellB = _board.Cells[xPos, yPos + 1];
                            if (cellB.CurrentGameObject != null)
                            {
                                _board.Cells[xPos, yPos + 1] = cellA;
                                _board.Cells[xPos, yPos] = cellB;

                                ICommand[] commands = {new SwipeUpCommand(cellA), new SwipeDownCommand(cellB)};
                                SetMacroCommand(commands);

                                OnEvent(EventTypes.Swipe, null);
                            }
                        }

                        break;

                    case MoveDirectionTypes.Down:
                        if (yPos > 0)
                        {
                            ICell cellB = _board.Cells[xPos, yPos - 1];
                            if (cellB.CurrentGameObject != null)
                            {
                                _board.Cells[xPos, yPos - 1] = cellA;
                                _board.Cells[xPos, yPos] = cellB;

                                ICommand[] commands = {new SwipeDownCommand(cellA), new SwipeUpCommand(cellB)};
                                SetMacroCommand(commands);

                                OnEvent(EventTypes.Swipe, null);
                            }
                        }

                        break;

                    case MoveDirectionTypes.Left:
                        if (xPos > 0)
                        {
                            ICell cellB = _board.Cells[xPos - 1, yPos];
                            if (cellB.CurrentGameObject != null)
                            {
                                _board.Cells[xPos - 1, yPos] = cellA;
                                _board.Cells[xPos, yPos] = cellB;

                                ICommand[] commands = {new SwipeLeftCommand(cellA), new SwipeRightCommand(cellB)};
                                SetMacroCommand(commands);

                                OnEvent(EventTypes.Swipe, null);
                            }
                        }

                        break;

                    case MoveDirectionTypes.Right:
                        if (xPos < _board.Width - 1)
                        {
                            ICell cellB = _board.Cells[xPos + 1, yPos];
                            if (cellB.CurrentGameObject != null)
                            {
                                _board.Cells[xPos + 1, yPos] = cellA;
                                _board.Cells[xPos, yPos] = cellB;

                                ICommand[] commands = {new SwipeRightCommand(cellA), new SwipeLeftCommand(cellB)};
                                SetMacroCommand(commands);

                                OnEvent(EventTypes.Swipe, null);
                            }
                        }

                        break;
                }
            }
        }

        #region Command Implimentation

        private void SetMacroCommand(ICommand[] commands)
        {
            _macroCommand = new MacroCommand(commands);
        }

        private void ExecuteMacroCommand()
        {
            _macroCommand.Execute();
        }

        private void UndoMacroCommand()
        {
            _macroCommand.Undo();
        }

        #endregion

        public IBoard Board
        {
            get { return _board; }
            set { _board = value; }
        }

        public ICheckManager CheckManager
        {
            get { return _checkManager; }
            set { _checkManager = value; }
        }

        public ISpawnManager SpawnManager
        {
            get { return _spawnManager; }
            set { _spawnManager = value; }
        }

        public int SpawnYPos
        {
            get { return _spawnYPos; }
            set { _spawnYPos = value; }
        }
    }
}