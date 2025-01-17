using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Loop;
using UnityEngine;
using Rnd = UnityEngine.Random;

public class LoopScript : MonoBehaviour
{
    public KMBombModule Module;
    public KMBombInfo BombInfo;
    public KMAudio Audio;

    public KMSelectable[] ArrowSels;
    public Texture[] ColorTextures;
    public GameObject[] ArrowObjs;

    private int _moduleId;
    private static int _moduleIdCounter = 1;
    private int _loopId;
    private static int _loopIdCounter = 1;
    private bool _moduleSolved;

    private const string primary = "_MainTex";
    private const string secondary = "_SecondTex";

    private const int _size = 4;
    private bool _canInteract = false;
    private int[] _arrowSolutions = new int[_size * _size];
    private int[] _currentArrowDirections = new int[_size * _size];
    private static readonly string[] _dirNames = new string[] { "U", "UR", "R", "DR", "D", "DL", "L", "UL" };
    private int? _currentlySelectedArrow;
    private readonly bool[] _isLit = new bool[_size * _size];
    private int[] _solutionPositionsForSolveAnim;
    private bool _valid;

    enum ArrowColor
    {
        Black,
        Border,
        White,
        DarkBlue,
        LightBlue,
        Green,
        Orange
    }

    private void Start()
    {
        _moduleId = _moduleIdCounter++;
        _loopId = _loopIdCounter++;
        for (int i = 0; i < ArrowSels.Length; i++)
        {
            ArrowSels[i].OnInteract += ArrowPress(i);
            ArrowSels[i].OnHighlight += ArrowHighlight(i);
            ArrowSels[i].OnHighlightEnded += ArrowHighlightEnded(i);
            ArrowObjs[i].transform.GetChild(0).GetComponent<MeshRenderer>().material.SetTexture(primary, ColorTextures[(int) ArrowColor.Black]);
            ArrowObjs[i].transform.GetChild(0).GetComponent<MeshRenderer>().material.SetTexture(secondary, ColorTextures[(int) ArrowColor.Border]);
            ArrowObjs[i].transform.GetChild(1).GetComponent<MeshRenderer>().material.SetTexture(primary, ColorTextures[(int) ArrowColor.Black]);
            ArrowObjs[i].transform.GetChild(1).GetComponent<MeshRenderer>().material.SetTexture(secondary, ColorTextures[(int) ArrowColor.DarkBlue]);
            ArrowObjs[i].transform.GetChild(2).GetComponent<MeshRenderer>().material.SetTexture(primary, ColorTextures[(int) ArrowColor.Black]);
            ArrowObjs[i].transform.GetChild(2).GetComponent<MeshRenderer>().material.SetTexture(secondary, ColorTextures[(int) ArrowColor.White]);
        }
        Module.OnActivate += Activate;

        _arrowSolutions = CreateLoop();
        Debug.LogFormat("[Loop #{0}] Solution: {1}", _moduleId, _arrowSolutions.Select(i => _dirNames[i]).Join(", "));
        _currentArrowDirections = _arrowSolutions.ToArray();
        while (IsValidPath(_currentArrowDirections) != null)
            _currentArrowDirections.Shuffle();
    }

    public static int CountBits(int input)
    {
        var result = 0;
        while (input != 0)
        {
            input &= unchecked(input - 1); // removes exactly one 1-bit (the least significant one)
            result++;
        }
        return result;
    }

    private static readonly int[] _connections = Enumerable.Range(0, 1 << 8).Where(i => CountBits(i) == 2).ToArray();

    private static IEnumerable<int[]> LoopGenerateRecurse(int?[] grid, bool[][] takens)
    {
        var ixs = new List<int>();
        for (var cell = 0; cell < grid.Length; cell++)
        {
            if (grid[cell] != null)
                continue;
            var c = takens[cell].Count(b => !b);
            if (c == 1)
            {
                ixs.Clear();
                ixs.Add(cell);
                goto shortcut;
            }
            if (c == 0)
                yield break;
            ixs.Add(cell);
        }

        if (ixs.Count == 0)
        {
            yield return grid.Select(v => v.Value).ToArray();
            yield break;
        }

        shortcut:
        var ix = ixs.PickRandom();
        var valOfs = Rnd.Range(0, _connections.Length);
        for (var iVal = 0; iVal < takens[ix].Length; iVal++)
        {
            var val = (iVal + valOfs) % _connections.Length;
            if (!takens[ix][val])
            {
                grid[ix] = _connections[val];

                var newTakens = takens.Select(ar => ar.ToArray()).ToArray();

                // Make sure that this placement doesn’t cause a premature loop
                var visited = 0;
                var curCell = ix;
                var prevDir = 0;
                do
                {
                    var otherDir = Enumerable.Range(0, 8).First(dir => dir != prevDir && (grid[curCell] & (1 << dir)) != 0);
                    curCell = new Coord(_size, _size, curCell).Neighbor((GridDirection) otherDir).Index;
                    prevDir = (otherDir + 4) % 8;
                    visited++;
                }
                while (curCell != ix && grid[curCell] != null);
                if (curCell == ix && visited < _size * _size)  // This would create a premature loop
                    goto busted;

                // Make sure that the neighboring cells connect to this one
                var cell = new Coord(_size, _size, ix);
                for (GridDirection dir = 0; dir < (GridDirection) 8; dir++)
                    if (cell.CanGoTo(dir))
                    {
                        var otherIx = cell.Neighbor(dir).Index;
                        for (var otherVal = 0; otherVal < newTakens[otherIx].Length; otherVal++)
                            if (((_connections[otherVal] & (1 << (((int) dir + 4) % 8))) != 0) != ((grid[ix] & (1 << (int) dir)) != 0))
                                newTakens[otherIx][otherVal] = true;
                    }

                foreach (var solution in LoopGenerateRecurse(grid, newTakens))
                    yield return solution;

                busted:
                grid[ix] = null;
            }
        }
    }

    private int[] CreateLoop()
    {
        var gridWithBitfields = LoopGenerateRecurse(
            grid: new int?[_size * _size],
            takens: NewArray(_size * _size, ix => NewArray(_connections.Length, c => !Enumerable.Range(0, 8).All(dir => (_connections[c] & (1 << dir)) == 0 || new Coord(_size, _size, ix).CanGoTo((GridDirection) dir))))
        ).First();

        var gridWithDirections = new int[_size * _size];

        var curCell = 0;
        var prevDir = 0;
        do
        {
            var otherDir = Enumerable.Range(0, 8).First(dir => dir != prevDir && (gridWithBitfields[curCell] & (1 << dir)) != 0);
            gridWithDirections[curCell] = otherDir;
            curCell = new Coord(_size, _size, curCell).Neighbor((GridDirection) otherDir).Index;
            prevDir = (otherDir + 4) % 8;
        }
        while (curCell != 0);
        return gridWithDirections;
    }

    public static T[] NewArray<T>(int size, Func<int, T> initialiser)
    {
        T[] array = new T[size];
        for (int i = 0; i < size; i++)
            array[i] = initialiser(i);
        return array;
    }

    private static List<int> IsValidPath(int[] path)
    {
        var visited = new List<int>();
        var curCell = 0;
        while (!visited.Contains(curCell))
        {
            visited.Add(curCell);
            var newCell = new Coord(_size, _size, curCell);
            if (!newCell.CanGoTo((GridDirection) path[curCell]))
                return null;
            curCell = newCell.Neighbor((GridDirection) path[curCell]).Index;
        }
        return visited.Count == _size * _size ? visited : null;
    }

    private KMSelectable.OnInteractHandler ArrowPress(int i)
    {
        return delegate ()
        {
            ArrowSels[i].AddInteractionPunch(0.25f);
            if (_moduleSolved || !_canInteract || _valid)
                return false;
            if (_currentlySelectedArrow == null)
            {
                Audio.PlaySoundAtTransform("Click", transform);
                _isLit[i] = true;
                _currentlySelectedArrow = i;
                ArrowObjs[i].transform.GetChild(1).GetComponent<MeshRenderer>().material.SetTexture(primary, ColorTextures[(int) ArrowColor.LightBlue]);
            }
            else if (_currentlySelectedArrow == i)
            {
                Audio.PlaySoundAtTransform("Click", transform);
                _isLit[i] = false;
                _currentlySelectedArrow = null;
                ArrowObjs[i].transform.GetChild(1).GetComponent<MeshRenderer>().material.SetTexture(primary, ColorTextures[(int) ArrowColor.DarkBlue]);
            }
            else
            {
                Audio.PlaySoundAtTransform("Swap", transform);
                _isLit[i] = true;
                _canInteract = false;
                ArrowObjs[i].transform.GetChild(1).GetComponent<MeshRenderer>().material.SetTexture(primary, ColorTextures[(int) ArrowColor.LightBlue]);
                int dirA = _currentArrowDirections[_currentlySelectedArrow.Value];
                int dirB = _currentArrowDirections[i];
                StartCoroutine(RotateArrow(_currentlySelectedArrow.Value, dirA, dirB));
                StartCoroutine(RotateArrow(i, dirB, dirA));
                _currentArrowDirections[_currentlySelectedArrow.Value] = dirB;
                _currentArrowDirections[i] = dirA;
                _currentlySelectedArrow = null;
                var validPath = IsValidPath(_currentArrowDirections);
                if (validPath != null)
                {
                    _solutionPositionsForSolveAnim = validPath.ToArray();
                    _valid = true;
                    Audio.PlaySoundAtTransform("Solve", transform);
                    StartCoroutine(SolveAnimation(i));
                }
            }
            return false;
        };
    }

    private Action ArrowHighlight(int i)
    {
        return delegate ()
        {
            if (!_canInteract)
                return;
            ArrowObjs[i].transform.GetChild(1).GetComponent<MeshRenderer>().material.SetTexture(primary, ColorTextures[(int) ArrowColor.Orange]);
        };
    }

    private Action ArrowHighlightEnded(int i)
    {
        return delegate ()
        {
            if (!_canInteract)
                return;
            ArrowObjs[i].transform.GetChild(1).GetComponent<MeshRenderer>().material.SetTexture(primary, ColorTextures[_isLit[i] ? (int) ArrowColor.LightBlue : (int) ArrowColor.DarkBlue]);
        };
    }

    private void Activate()
    {
        if (_loopId == 1)
            Audio.PlaySoundAtTransform("Startup", transform);
        StartCoroutine(StartAnimation());
    }

    private void OnDestroy()
    {
        _loopIdCounter = 1;
    }

    private IEnumerator StartAnimation()
    {
        for (int i = 0; i < (_size * _size); i++)
        {
            StartCoroutine(FadeArrowIn(i));
            yield return new WaitForSeconds(0.15f);
        }
    }

    private IEnumerator RotateArrow(int i, int dirStart, int dirEnd)
    {
        var elapsed = 0f;
        var duration = 0.5f;
        float rotStart = 45f * dirStart;
        float rotEnd = 45f * dirEnd;
        if (dirStart - dirEnd > 4)
            rotEnd += 360f;
        if (dirEnd - dirStart > 4)
            rotStart += 360f;
        while (elapsed < duration)
        {
            ArrowObjs[i].transform.localEulerAngles = new Vector3(0, Easing.InOutQuad(elapsed, rotStart, rotEnd, duration), 0);
            yield return null;
            elapsed += Time.deltaTime;
        }
        ArrowObjs[i].transform.localEulerAngles = new Vector3(0, (rotEnd + 360f) % 360f, 0);
        ArrowObjs[i].transform.GetChild(1).GetComponent<MeshRenderer>().material.SetTexture(primary, ColorTextures[(int) ArrowColor.DarkBlue]);
        _isLit[i] = false;
        _canInteract = true;
    }

    private IEnumerator FadeArrowIn(int i)
    {
        var elapsed = 0f;
        var duration = 1f;
        while (elapsed < duration)
        {
            ArrowObjs[i].transform.GetChild(0).GetComponent<MeshRenderer>().material.SetFloat("_Blend", elapsed / duration);
            ArrowObjs[i].transform.GetChild(1).GetComponent<MeshRenderer>().material.SetFloat("_Blend", elapsed / duration);
            ArrowObjs[i].transform.GetChild(2).GetComponent<MeshRenderer>().material.SetFloat("_Blend", elapsed / duration);
            ArrowObjs[i].transform.localEulerAngles = new Vector3(0, Easing.OutQuad(elapsed, 0, 360f + _currentArrowDirections[i] * 45f, duration), 0f);
            yield return null;
            elapsed += Time.deltaTime;
        }
        ArrowObjs[i].transform.GetChild(0).GetComponent<MeshRenderer>().material.SetFloat("_Blend", 1);
        ArrowObjs[i].transform.GetChild(1).GetComponent<MeshRenderer>().material.SetFloat("_Blend", 0);
        ArrowObjs[i].transform.GetChild(2).GetComponent<MeshRenderer>().material.SetFloat("_Blend", 1);
        ArrowObjs[i].transform.GetChild(1).GetComponent<MeshRenderer>().material.SetTexture(primary, ColorTextures[(int) ArrowColor.DarkBlue]);
        ArrowObjs[i].transform.GetChild(1).GetComponent<MeshRenderer>().material.SetTexture(secondary, ColorTextures[(int) ArrowColor.Green]);
        if (i == _size - 1)
            _canInteract = true;
    }

    private List<int> GetValidDirs(int pos)
    {
        int r = pos / _size;
        int c = pos % _size;
        var list = new List<int>();
        if (r != 0)
            list.Add(0);
        if (r != 0 && c != _size - 1)
            list.Add(1);
        if (c != _size - 1)
            list.Add(2);
        if (r != _size - 1 && c != _size - 1)
            list.Add(3);
        if (r != _size - 1)
            list.Add(4);
        if (r != _size - 1 && c != 0)
            list.Add(5);
        if (c != 0)
            list.Add(6);
        if (r != 0 && c != 0)
            list.Add(7);
        return list;
    }

    private int GetNewPos(int pos, int dir)
    {
        if (dir == 0)
            return pos - _size;
        if (dir == 1)
            return pos - _size + 1;
        if (dir == 2)
            return pos + 1;
        if (dir == 3)
            return pos + _size + 1;
        if (dir == 4)
            return pos + _size;
        if (dir == 5)
            return pos + _size - 1;
        if (dir == 6)
            return pos - 1;
        if (dir == 7)
            return pos - _size + 1;
        throw new InvalidOperationException("idunno");
    }

    private IEnumerator SolveAnimation(int st)
    {
        int ix = Array.IndexOf(_solutionPositionsForSolveAnim, st);
        for (int i = ix; i < (ix + _size * _size); i++)
        {
            StartCoroutine(SolveArrowTransition(_solutionPositionsForSolveAnim[i % (_size * _size)]));
            yield return new WaitForSeconds(0.3f);
        }
        yield return new WaitForSeconds(0.25f);
        _moduleSolved = true;
        Module.HandlePass();
    }

    private IEnumerator SolveArrowTransition(int i)
    {
        var duration = 0.7f;
        var elapsed = 0f;
        while (elapsed < duration)
        {
            ArrowObjs[i].transform.GetChild(1).GetComponent<MeshRenderer>().material.SetFloat("_Blend", elapsed / duration);
            yield return null;
            elapsed += Time.deltaTime;
        }
        ArrowObjs[i].transform.GetChild(1).GetComponent<MeshRenderer>().material.SetFloat("_Blend", 1);
    }

#pragma warning disable 0414
    private readonly string TwitchHelpMessage = "!{0} swap a1 b2 [Swap positions A1 and B2.] | Acceptable commands include A1-C3, TL-BR, 1-9.";
#pragma warning disable 0414
    private IEnumerator ProcessTwitchCommand(string command)
    {
        command = command.Trim().ToLowerInvariant();
        if (!command.StartsWith("swap "))
            yield break;
        command = command.Substring(4);
        var inputs = command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToArray();
        if (inputs.Length % 2 != 0)
        {
            yield return "sendtochaterror An odd number of inputs has been entered. Invalid command.";
            yield break;
        }
        var list = new List<int>();
        for (int i = 0; i < inputs.Length; i++)
        {
            if (inputs[i] == "1" || inputs[i] == "tl" || inputs[i] == "a1")
                list.Add(0);
            else if (inputs[i] == "2" || inputs[i] == "tm" || inputs[i] == "b1")
                list.Add(1);
            else if (inputs[i] == "3" || inputs[i] == "tr" || inputs[i] == "c1")
                list.Add(2);
            else if (inputs[i] == "4" || inputs[i] == "ml" || inputs[i] == "a2")
                list.Add(3);
            else if (inputs[i] == "5" || inputs[i] == "mm" || inputs[i] == "b2")
                list.Add(4);
            else if (inputs[i] == "6" || inputs[i] == "mr" || inputs[i] == "c2")
                list.Add(5);
            else if (inputs[i] == "7" || inputs[i] == "bl" || inputs[i] == "a3")
                list.Add(6);
            else if (inputs[i] == "8" || inputs[i] == "bm" || inputs[i] == "b3")
                list.Add(7);
            else if (inputs[i] == "9" || inputs[i] == "br" || inputs[i] == "c3")
                list.Add(8);
            else
            {
                yield return "sendtochaterror \"" + inputs[i] + "\" is an invalid input.";
                yield break;
            }
        }
        yield return null;
        yield return "solve";
        for (int i = 0; i < list.Count; i++)
        {
            while (!_canInteract)
                yield return null;
            ArrowSels[list[i]].OnInteract();
            yield return new WaitForSeconds(0.1f);
        }
    }

    private IEnumerator TwitchHandleForcedSolve()
    {
        while (!_canInteract)
            yield return null;
        for (int i = 0; i < (_size * _size); i++)
        {
            if (_currentArrowDirections[i] != _arrowSolutions[i])
            {
                for (int j = i; j < (_size * _size); j++)
                    if (_currentArrowDirections[j] == _arrowSolutions[i])
                    {
                        ArrowSels[i].OnInteract();
                        yield return new WaitForSeconds(0.1f);
                        ArrowSels[j].OnInteract();
                        yield return new WaitForSeconds(0.1f);
                        while (!_canInteract)
                            yield return null;
                        goto nextIter;
                    }
            }
            nextIter:;
        }
        while (!_moduleSolved)
            yield return true;
    }
}
