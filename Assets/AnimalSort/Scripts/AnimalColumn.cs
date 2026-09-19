using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class AnimalColumn : MonoBehaviour, IPointerClickHandler
{
    public int Index { get; private set; }
    public int MaxHeight { get; private set; } = 5;
    public int Count => pieces.Count;
    public IReadOnlyList<AnimalPiece> Pieces => pieces;

    readonly List<AnimalPiece> pieces = new List<AnimalPiece>();
    SortGame game;
    Vector3 topPosition;
    float cellSize = 1.3f;

    public void Setup(SortGame owner, int index, int maxHeight, Vector3 top, float cell)
    {
        game = owner;
        Index = index;
        MaxHeight = maxHeight;
        cellSize = cell;
        topPosition = top;
        transform.position = top;
        name = "Column_" + index;
    }

    public bool IsEmpty => pieces.Count == 0;
    public bool IsFull => pieces.Count >= MaxHeight;
    public AnimalPiece Top => IsEmpty ? null : pieces[0];
    public AnimalPiece Bottom => IsEmpty ? null : pieces[pieces.Count - 1];

    public bool IsSolved
    {
        get
        {
            if (pieces.Count != MaxHeight) return false;
            AnimalType type = pieces[0].Type;
            if (type == AnimalType.Rainbow) return false;
            for (int i = 1; i < pieces.Count; i++)
            {
                if (pieces[i].Type != type) return false;
            }
            return true;
        }
    }

    public Vector3 SlotWorld(int index)
    {
        return topPosition + Vector3.down * (index * cellSize);
    }

    public void Push(AnimalPiece piece)
    {
        pieces.Add(piece);
        Snap(piece, pieces.Count - 1);
    }

    public AnimalPiece PopBottom()
    {
        if (IsEmpty) return null;

        int last = pieces.Count - 1;
        AnimalPiece piece = pieces[last];
        pieces.RemoveAt(last);
        Detach(piece);
        return piece;
    }

    public AnimalPiece ShiftInFromTop(AnimalPiece incoming)
    {
        AnimalPiece outgoing = null;
        if (pieces.Count > 0)
        {
            int last = pieces.Count - 1;
            outgoing = pieces[last];
            pieces.RemoveAt(last);
            Detach(outgoing);
        }

        pieces.Insert(0, incoming);
        incoming.transform.SetParent(transform, true);
        return outgoing;
    }

    public AnimalPiece ShiftInFromBottom(AnimalPiece incoming)
    {
        AnimalPiece outgoing = null;
        if (pieces.Count > 0)
        {
            outgoing = pieces[0];
            pieces.RemoveAt(0);
            Detach(outgoing);
        }

        pieces.Add(incoming);
        incoming.transform.SetParent(transform, true);
        return outgoing;
    }

    public void SnapAll()
    {
        for (int i = 0; i < pieces.Count; i++)
            Snap(pieces[i], i);
    }

    public void Snap(AnimalPiece piece, int index)
    {
        if (piece == null) return;
        piece.transform.SetParent(transform, true);
        piece.transform.position = SlotWorld(index);
        piece.transform.localRotation = Quaternion.identity;
        piece.transform.localScale = Vector3.one;
        piece.SetSorting(10 + index);
    }

    void Detach(AnimalPiece piece)
    {
        piece.transform.SetParent(null, true);
        piece.SetSorting(60);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (game != null)
            game.ClickColumn(this);
    }
}
