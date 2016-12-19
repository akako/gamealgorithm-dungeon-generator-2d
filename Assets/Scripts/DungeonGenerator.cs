using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DungeonGenerator : MonoBehaviour
{
    [Range(4, 100)]
    public int width;
    [Range(4, 100)]
    public int height;
    [SerializeField]
    RoomSettings roomSettings;

    [Serializable]
    public class RoomSettings
    {
        [Range(2, 10)]
        public int minWidth;
        [Range(2, 10)]
        public int minHeight;
        [Range(0, 100)]
        public int bigRoomRate;
        [Range(1, 10)]
        public int maxWallThicknessInArea;
    }

    [SerializeField]

    /// <summary>
    /// ダンジョンマップを生成します
    /// </summary>
    public int[,] Generate()
    {
        int[,] map = new int[width, height];
        var baseArea = new Area(0, 0, width, height, roomSettings);
        // Areaを分割
        var dividedAreas = baseArea.Divide();
        // Areaを描画
        foreach (var area in dividedAreas)
        {
            map = area.WriteToMap(map);
        }
        // Area同士を繋ぐ通路を作る
        var passages = GeneratePassagesByArea(dividedAreas);
        // 通路を描画
        foreach (var passage in passages)
        {
            map = passage.WriteToMap(map);
        }

        return map;
    }

    /// <summary>
    /// エリアを繋ぐ通路を生成します
    /// </summary>
    /// <returns>The passages by area.</returns>
    /// <param name="areas">Areas.</param>
    Passage[] GeneratePassagesByArea(Area[] areas)
    {
        // 隣接したエリアが繋がるよう通路を生成
        var passages = new List<Passage>();
        foreach (var area1 in areas)
        {
            foreach (var area2 in areas)
            {
                if (area1 == area2 || !IsAdjacently(area1, area2))
                {
                    continue;
                }

                passages.Add(new Passage(area1, area2));
            }
        }

        // 不要な通路を消していく
        var fixedPassages = new List<Passage>();
        while (passages.Count > 0)
        {
            // 通路をランダムでひとつ削除
            var targetIndex = UnityEngine.Random.Range(0, passages.Count);
            var targetPassage = passages[targetIndex];
            passages.RemoveAt(targetIndex);

            // 全エリアが繋がっているかチェック
            if (!IsAllAreaConnected(areas.ToList(), passages.ToArray(), fixedPassages.ToArray()))
            {
                // 削除したことでエリアがバラけてしまった。つまり消すわけにはいかない重要な通路なので保持
                fixedPassages.Add(targetPassage);
            }
        }
        return fixedPassages.ToArray();
    }

    /// <summary>
    /// エリア同士が隣接しているかチェックします
    /// </summary>
    /// <returns><c>true</c> if this instance is adjacently the specified area1 area2; otherwise, <c>false</c>.</returns>
    /// <param name="area1">Area1.</param>
    /// <param name="area2">Area2.</param>
    bool IsAdjacently(Area area1, Area area2)
    {
        // Areaの位置関係をチェック
        var left = area1.x < area2.x ? area1 : area2;
        var right = area1.x > area2.x ? area1 : area2;
        var top = area1.y > area2.y ? area1 : area2;
        var bottom = area1.y < area2.y ? area1 : area2;

        // 左右に接しているかどうかのチェック
        if (null != left && null != right &&
            (left.x + left.width) == right.x &&
            (left.y <= right.y && right.y < (left.y + left.height) || right.y <= left.y && left.y < (right.y + right.height)))
        {
            return true;
        }

        // 上下に接しているかどうかのチェック
        if (null != top && null != bottom &&
            (bottom.y + bottom.height) == top.y &&
            (bottom.x <= top.x && top.x < (bottom.x + bottom.width) || top.x <= bottom.x && bottom.x < (top.x + top.width)))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 全てのエリアが繋がっているかどうかチェックします
    /// </summary>
    /// <returns><c>true</c> if this instance is all area connected the specified areas passages1 passages2; otherwise, <c>false</c>.</returns>
    /// <param name="areas">Areas.</param>
    /// <param name="passages1">Passages1.</param>
    /// <param name="passages2">Passages2.</param>
    bool IsAllAreaConnected(List<Area> areas, Passage[] passages1, Passage[] passages2)
    {
        if (areas.Count <= 1)
        {
            return true;
        }

        var passages = new List<Passage>();
        passages.AddRange(passages1);
        passages.AddRange(passages2);

        // エリア[0]をチェック対象とし、チェック開始
        var checkingAreas = new List<Area>(){ areas[0] };
        areas.RemoveAt(0);
        var checkedAreas = new List<Area>(){ };

        while (checkingAreas.Count > 0)
        {
            var nextCheckTargetAreas = new List<Area>(){ };
            foreach (var checkTargetArea in checkingAreas)
            {
                // チェック対象のエリアから伸びる通路を取得
                foreach (var passage in passages.Where(x => x.areas.Contains(checkTargetArea)))
                {
                    // チェック対象のエリアから、通路でつながれたエリアを取得
                    var pairedArea = passage.areas.First(x => x != checkTargetArea);
                    if (!checkedAreas.Contains(pairedArea) && !checkingAreas.Contains(pairedArea) && !nextCheckTargetAreas.Contains(pairedArea))
                    {
                        // 通路でつながれたエリアはareasから除去、次回のチェック対象エリアにする
                        areas.Remove(pairedArea);
                        nextCheckTargetAreas.Add(pairedArea);
                    }
                }
            }
            checkedAreas.AddRange(checkingAreas);
            checkingAreas = nextCheckTargetAreas;
        }

        // areasから全てのエリアが除去されたならば、全てのエリアが繋がっているということになる
        return areas.Count == 0;
    }

    /// <summary>
    /// エリアクラス
    /// </summary>
    class Area
    {
        public readonly int x;
        public readonly int y;
        public readonly int width;
        public readonly int height;
        public readonly RoomSettings roomSettings;
        public readonly Room room;

        /// <summary>
        /// エリアを分割可能かどうか
        /// </summary>
        /// <value><c>true</c> if this instance is dividable; otherwise, <c>false</c>.</value>
        bool IsDividable
        {
            get { return IsDividableHorizontal || IsDividableVertical; }
        }

        /// <summary>
        /// エリアを横に分割可能かどうか
        /// </summary>
        /// <value><c>true</c> if this instance is dividable horizontal; otherwise, <c>false</c>.</value>
        bool IsDividableHorizontal
        {
            get { return MinWidth * 2 <= width; }
        }

        /// <summary>
        /// エリアを縦に分割可能かどうか
        /// </summary>
        /// <value><c>true</c> if this instance is dividable vertical; otherwise, <c>false</c>.</value>
        bool IsDividableVertical
        {
            get { return MinHeight * 2 <= height; }
        }

        /// <summary>
        /// エリア幅の最小値
        /// </summary>
        /// <value>The minimum width.</value>
        int MinWidth
        {
            get { return roomSettings.minWidth + 2; }
        }

        /// <summary>
        /// エリア高さの最大値
        /// </summary>
        /// <value>The minimum height.</value>
        int MinHeight
        {
            get { return roomSettings.minHeight + 2; }
        }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="x">The x coordinate.</param>
        /// <param name="y">The y coordinate.</param>
        /// <param name="width">Width.</param>
        /// <param name="height">Height.</param>
        /// <param name="roomSettings">Room settings.</param>
        public Area(int x, int y, int width, int height, RoomSettings roomSettings)
        {
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
            this.roomSettings = roomSettings;
            this.room = GenerateRoom();
        }

        /// <summary>
        /// エリアを分割します
        /// </summary>
        public Area[] Divide()
        {
            var dividableAreas = new Area[]{ this };
            var devidedAreas = new List<Area>();
            var fixedAreas = new List<Area>{ };

            // Area分割を繰り返す
            while (true)
            {
                // 分割不可能なエリアはfixedに入れる
                fixedAreas.AddRange(dividableAreas.Where(x => !x.IsDividable));

                if (dividableAreas.Length == 0)
                {
                    // 分割可能なエリアが無いならループを抜ける
                    break;
                }

                devidedAreas.Clear();
                // 分割可能なエリアは分割を試みる
                foreach (var area in dividableAreas.Where(x => x.IsDividable))
                {
                    if (UnityEngine.Random.Range(0, 100) < roomSettings.bigRoomRate)
                    {
                        // ある程度部屋を分割済みの時、一定確率でエリアを分割せずそのまま部屋にする
                        fixedAreas.Add(area);
                    }
                    else
                    {
                        devidedAreas.AddRange(area.DivideOnceIfPossible());
                    }
                }
                dividableAreas = devidedAreas.ToArray();
            }

            return fixedAreas.ToArray();
        }

        /// <summary>
        /// マップに部屋を書き込みます
        /// </summary>
        /// <param name="map">Map.</param>
        public int[,] WriteToMap(int[,] map)
        {
            for (int dx = room.x; dx < room.x + room.width; dx++)
            {
                for (int dy = room.y; dy < room.y + room.height; dy++)
                {
                    map[dx, dy] = 1;
                }
            }
            return map;
        }

        /// <summary>
        /// エリア内に部屋を生成します
        /// </summary>
        /// <returns>The room.</returns>
        Room GenerateRoom()
        {
            var left = UnityEngine.Random.Range(1, Math.Min(1 + roomSettings.maxWallThicknessInArea, width - roomSettings.minWidth));
            var right = UnityEngine.Random.Range(Math.Max(width - roomSettings.maxWallThicknessInArea, left + roomSettings.minWidth), width - 1);
            var bottom = UnityEngine.Random.Range(1, Math.Min(1 + roomSettings.maxWallThicknessInArea, height - roomSettings.minHeight));
            var top = UnityEngine.Random.Range(Math.Max(height - roomSettings.maxWallThicknessInArea, bottom + roomSettings.minHeight), height - 1);
            return new Room(x + left, y + bottom, right - left, top - bottom);
        }

        /// <summary>
        /// 可能であればエリアを1回だけ分割します
        /// </summary>
        /// <returns>The once.</returns>
        Area[] DivideOnceIfPossible()
        {
            if (IsDividableHorizontal && IsDividableVertical && UnityEngine.Random.Range(0, 2) == 0 || IsDividableHorizontal && !IsDividableVertical)
            {
                // 左右に分割
                var dividePosX = UnityEngine.Random.Range(x + MinWidth, x + width - MinWidth + 1);
                return new Area[]
                {
                    new Area(x, y, dividePosX - x, height, roomSettings),
                    new Area(dividePosX, y, width - (dividePosX - x), height, roomSettings)
                };
            }
            else if (IsDividableVertical)
            {
                // 上下に分割
                var dividePosY = UnityEngine.Random.Range(y + MinHeight, y + height - MinHeight + 1);
                return new Area[]
                {
                    new Area(x, y, width, dividePosY - y, roomSettings),
                    new Area(x, dividePosY, width, height - (dividePosY - y), roomSettings)
                };
            }
            else
            {
                // 分割不能ならそのまま返す
                return new Area[]{ this };
            }
        }

        /// <summary>
        /// 部屋クラス
        /// </summary>
        public class Room
        {
            public readonly int x;
            public readonly int y;
            public readonly int width;
            public readonly int height;

            public Room(int x, int y, int width, int height)
            {
                this.x = x;
                this.y = y;
                this.width = width;
                this.height = height;
            }
        }
    }

    /// <summary>
    /// 通路クラス
    /// </summary>
    class Passage
    {
        /// <summary>
        /// 通路で繋ぐエリア
        /// </summary>
        public readonly Area[] areas;

        public Passage(Area area1, Area area2)
        {
            this.areas = new Area[]{ area1, area2 };
        }

        /// <summary>
        /// マップに通路を書き込みます
        /// </summary>
        /// <returns>The to map.</returns>
        /// <param name="map">Map.</param>
        public int[,] WriteToMap(int[,] map)
        {
            var fromX = UnityEngine.Random.Range(areas[0].room.x, areas[0].room.x + areas[0].room.width);
            var fromY = UnityEngine.Random.Range(areas[0].room.y, areas[0].room.y + areas[0].room.height);
            var toX = UnityEngine.Random.Range(areas[1].room.x, areas[1].room.x + areas[1].room.width);
            var toY = UnityEngine.Random.Range(areas[1].room.y, areas[1].room.y + areas[1].room.height);
            while (fromX != toX || fromY != toY)
            {
                map[fromX, fromY] = 1;
                if (fromX != toX && fromY != toY && UnityEngine.Random.Range(0, 2) == 0 || fromY == toY)
                {
                    fromX += (toX - fromX) > 0 ? 1 : -1;
                }
                else
                {
                    fromY += (toY - fromY) > 0 ? 1 : -1;
                }
            }
            return map;
        }
    }
}
