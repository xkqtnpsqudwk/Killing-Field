using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.CompilerServices;
using My2DEngine.Game.Config;

namespace My2DEngine.Rendering.WorldData
{
    /// <summary>
    /// 아틀라스 슬롯 할당과 픽셀 더티 상태를 추적하는 캐시.
    /// 매 프레임 사용된 슬롯과 변경된 픽셀만 GPU에 업로드하여 대역폭을 절감한다.
    /// </summary>
    internal sealed class SpriteAtlasCache
    {
        /// <summary>스프라이트 한 칸 크기(픽셀).</summary>
        public const int CellSize = RenderConfig.TextureSize;

        /// <summary>칸 둘레 여백. 가장자리 색을 복제해 확대·회전 때 이웃 칸이 섞이지 않게 한다.</summary>
        public const int Padding = 1;

        /// <summary>여백 포함 칸 간격.</summary>
        public const int CellStride = CellSize + (Padding * 2);

        /// <summary>아틀라스 전체 ARGB 픽셀 배열.</summary>
        public int[] Pixels;

        /// <summary>변경된 셀 영역을 기록하는 정수 배열(x, y, w, h 순으로 4개씩).</summary>
        public int[] DirtyRects;

        /// <summary>DirtyRects에 기록된 더티 사각형의 수.</summary>
        public int DirtyRectCount;

        /// <summary>아틀라스 전체 재업로드가 필요하면 true. 레이아웃이 변경된 경우 설정된다.</summary>
        public bool UploadFullAtlas;

        /// <summary>이번 프레임에서 사용된 슬롯의 최대 인덱스 + 1. 아틀라스 크기 계산에 사용된다.</summary>
        public int ActiveSlotSpan;

        /// <summary>각 슬롯의 소유자 오브젝트 참조. null이면 해당 슬롯이 비어 있음을 나타낸다.</summary>
        public object[] PreviousOwners;

        /// <summary>이번 프레임에 각 슬롯이 사용되었는지 여부. BeginFrame에서 초기화된다.</summary>
        public bool[] PreviousUsed;

        /// <summary>각 슬롯의 이전 프레임 스프라이트 픽셀 소스. 변경 감지에 사용된다.</summary>
        public Color[][] PreviousSpriteSources;

        /// <summary>각 슬롯의 이전 프레임 피격 색조 플래그. 변경 감지에 사용된다.</summary>
        public bool[] PreviousSpriteTintFlags;

        /// <summary>유효한 슬롯 수(슬롯 인덱스의 상한). 아틀라스 치수 계산에 사용된다.</summary>
        public int PreviousSpriteCount;

        /// <summary>마지막 아틀라스 빌드 시의 너비(픽셀). 레이아웃 변경 감지에 사용된다.</summary>
        public int PreviousAtlasWidth;

        /// <summary>마지막 아틀라스 빌드 시의 높이(픽셀). 레이아웃 변경 감지에 사용된다.</summary>
        public int PreviousAtlasHeight;

        /// <summary>소유자 오브젝트 참조를 슬롯 인덱스로 매핑한다.</summary>
        public readonly Dictionary<object, int> Slots;

        /// <summary>해제된 슬롯 인덱스를 보관하는 전역 free-list 스택이다.</summary>
        public readonly Stack<int> FreeSlots;

        /// <summary>새 슬롯 할당 시 사용할 다음 전역 슬롯 인덱스다.</summary>
        public int NextSlot;

        /// <summary>
        /// SpriteAtlasCache를 초기화한다.
        /// 슬롯 맵과 free-list를 초기화한다.
        /// </summary>
        public SpriteAtlasCache()
        {
            Slots = new Dictionary<object, int>(ReferenceComparer.Instance);
            FreeSlots = new Stack<int>();
        }

        /// <summary>
        /// 새 프레임 시작 시 아틀라스 캐시의 PreviousUsed 배열을 초기화한다.
        /// ReleaseUnusedSlots에서 이 프레임에 사용되지 않은 슬롯을 해제할 때 기준이 된다.
        /// </summary>
        public void BeginFrame()
        {
            if (PreviousUsed != null && PreviousSpriteCount > 0)
            {
                Array.Clear(PreviousUsed, 0, PreviousSpriteCount);
            }
        }

        /// <summary>
        /// 이번 프레임에 사용되지 않은 아틀라스 슬롯의 소유자 참조를 해제하고 free slot으로 반환한다.
        /// </summary>
        public void ReleaseUnusedSlots()
        {
            for (int i = 0; i < PreviousSpriteCount; i++)
            {
                if (PreviousOwners[i] != null && !PreviousUsed[i])
                {
                    Slots.Remove(PreviousOwners[i]);
                    PreviousOwners[i] = null;
                    PreviousSpriteSources[i] = null;
                    PreviousSpriteTintFlags[i] = false;
                    FreeSlots.Push(i);
                }
            }
        }

        /// <summary>
        /// 슬롯 배열의 뒤쪽에 남아 있는 빈 영역을 잘라내 다음 프레임 아틀라스 크기가 줄어들 수 있게 한다.
        /// </summary>
        public void TrimUnusedTail()
        {
            if (PreviousOwners == null || PreviousSpriteCount <= 0)
            {
                PreviousSpriteCount = 0;
                return;
            }

            int trimmedCount = PreviousSpriteCount;
            while (trimmedCount > 0 && PreviousOwners[trimmedCount - 1] == null)
            {
                trimmedCount--;
            }

            PreviousSpriteCount = trimmedCount;
        }

        /// <summary>
        /// owner 오브젝트에 대한 아틀라스 슬롯을 반환하거나 새로 할당한다.
        /// 같은 owner가 이미 슬롯을 가지고 있으면 기존 슬롯을 반환한다.
        /// 빈 슬롯이 있으면 재사용하고, 없으면 전역 카운터를 증가시켜 새 슬롯을 할당한다.
        /// </summary>
        /// <param name="owner">슬롯 소유자(적, 투사체, 픽업 등의 오브젝트 참조).</param>
        /// <returns>할당된 아틀라스 슬롯 인덱스.</returns>
        public int GetOrCreateSlot(object owner)
        {
            if (owner == null)
            {
                return 0;
            }

            if (Slots.TryGetValue(owner, out int existingSlot))
            {
                return existingSlot;
            }

            int slot;
            if (FreeSlots.Count > 0)
            {
                slot = FreeSlots.Pop();
            }
            else
            {
                slot = NextSlot++;
            }

            EnsureSlotCapacity(slot + 1);
            if (slot >= PreviousSpriteCount)
            {
                PreviousSpriteCount = slot + 1;
            }

            PreviousOwners[slot] = owner;
            Slots[owner] = slot;
            return slot;
        }

        /// <summary>
        /// 아틀라스 캐시의 슬롯 관련 배열이 requiredSlots 이상의 크기를 가지도록 확장한다.
        /// 부족하면 두 배씩 확장한다.
        /// </summary>
        /// <param name="requiredSlots">필요한 최소 슬롯 수.</param>
        public void EnsureSlotCapacity(int requiredSlots)
        {
            if (PreviousOwners == null || PreviousOwners.Length < requiredSlots)
            {
                int newLength = PreviousOwners == null ? 16 : PreviousOwners.Length * 2;
                while (newLength < requiredSlots)
                {
                    newLength *= 2;
                }

                Array.Resize(ref PreviousOwners, newLength);
                Array.Resize(ref PreviousUsed, newLength);
                Array.Resize(ref PreviousSpriteSources, newLength);
                Array.Resize(ref PreviousSpriteTintFlags, newLength);
            }
        }

        /// <summary>
        /// 아틀라스 캐시의 Pixels 배열이 atlasWidth × atlasHeight 이상의 크기를 가지도록 확장한다.
        /// 크기가 부족하면 새로 할당하고 UploadFullAtlas를 true로 설정한다.
        /// </summary>
        /// <param name="atlasWidth">필요한 아틀라스 너비(픽셀).</param>
        /// <param name="atlasHeight">필요한 아틀라스 높이(픽셀).</param>
        public void EnsurePixelCapacity(int atlasWidth, int atlasHeight)
        {
            int atlasPixelCount = atlasWidth * atlasHeight;
            if (Pixels == null || Pixels.Length < atlasPixelCount)
            {
                Pixels = new int[atlasPixelCount];
                PreviousAtlasWidth = atlasWidth;
                PreviousAtlasHeight = atlasHeight;
                UploadFullAtlas = true;
            }
        }

        /// <summary>
        /// 방 전환 시 아틀라스 캐시 전체를 비워 큰 버퍼를 해제한다.
        /// </summary>
        public void Reset()
        {
            Pixels = null;
            DirtyRects = null;
            DirtyRectCount = 0;
            UploadFullAtlas = false;
            ActiveSlotSpan = 0;
            PreviousOwners = null;
            PreviousUsed = null;
            PreviousSpriteSources = null;
            PreviousSpriteTintFlags = null;
            PreviousSpriteCount = 0;
            PreviousAtlasWidth = 0;
            PreviousAtlasHeight = 0;
            Slots.Clear();
            FreeSlots.Clear();
            NextSlot = 0;
        }

        /// <summary>
        /// 아틀라스 캐시의 DirtyRects 배열에 변경된 셀 영역을 추가한다.
        /// 배열 용량이 부족하면 두 배로 확장한다.
        /// </summary>
        /// <param name="x">더티 영역의 왼쪽 X 좌표(아틀라스 픽셀 기준).</param>
        /// <param name="y">더티 영역의 위쪽 Y 좌표(아틀라스 픽셀 기준).</param>
        /// <param name="width">더티 영역의 너비(픽셀).</param>
        /// <param name="height">더티 영역의 높이(픽셀).</param>
        public void AddDirtyRect(int x, int y, int width, int height)
        {
            int requiredLength = (DirtyRectCount + 1) * 4;
            if (DirtyRects == null || DirtyRects.Length < requiredLength)
            {
                int newLength = DirtyRects == null ? 16 : DirtyRects.Length * 2;
                while (newLength < requiredLength)
                {
                    newLength *= 2;
                }

                Array.Resize(ref DirtyRects, newLength);
            }

            int baseIndex = DirtyRectCount * 4;
            DirtyRects[baseIndex + 0] = x;
            DirtyRects[baseIndex + 1] = y;
            DirtyRects[baseIndex + 2] = width;
            DirtyRects[baseIndex + 3] = height;
            DirtyRectCount++;
        }

        /// <summary>
        /// 스프라이트 수에 따라 아틀라스의 열·행 수를 결정하고 픽셀 크기를 반환한다.
        /// 가능한 한 정사각형에 가까운 레이아웃을 사용한다.
        /// </summary>
        /// <param name="spriteCount">아틀라스에 배치할 스프라이트 수.</param>
        /// <param name="width">계산된 아틀라스 너비(픽셀). spriteCount가 0 이하이면 0.</param>
        /// <param name="height">계산된 아틀라스 높이(픽셀). spriteCount가 0 이하이면 0.</param>
        public static void GetDimensions(int spriteCount, out int width, out int height)
        {
            if (spriteCount <= 0)
            {
                width = 0;
                height = 0;
                return;
            }

            int columns = (int)Math.Ceiling(Math.Sqrt(spriteCount));
            int rows = (spriteCount + columns - 1) / columns;
            width = columns * CellStride;
            height = rows * CellStride;
        }

        /// <summary>
        /// 스프라이트 픽셀 배열을 아틀라스 버퍼의 지정 셀에 복사한다.
        /// 패딩 픽셀에는 가장자리 색을 복제하여 확대/회전 시 이웃 셀이 섞이지 않도록 한다.
        /// tintHitFlash가 true이면 불투명 픽셀의 R 채널을 255로 올려 피격 효과를 표현한다.
        /// </summary>
        /// <param name="atlasPixels">대상 아틀라스 ARGB 픽셀 배열.</param>
        /// <param name="spritePixels">복사할 소스 스프라이트 Color[] 픽셀 배열.</param>
        /// <param name="atlasCellX">아틀라스 내 셀의 왼쪽 X 좌표(패딩 포함).</param>
        /// <param name="atlasCellY">아틀라스 내 셀의 위쪽 Y 좌표(패딩 포함).</param>
        /// <param name="atlasWidth">아틀라스 이미지의 너비(픽셀).</param>
        /// <param name="tintHitFlash">true이면 불투명 픽셀에 피격 빨강 색조를 적용한다.</param>
        public static void CopySprite(int[] atlasPixels, Color[] spritePixels, int atlasCellX, int atlasCellY, int atlasWidth, bool tintHitFlash)
        {
            if (spritePixels == null)
            {
                return;
            }

            int atlasX = atlasCellX + Padding;
            int atlasY = atlasCellY + Padding;
            for (int y = 0; y < CellSize; y++)
            {
                int sourceRow = y * CellSize;
                int atlasRow = (atlasY + y) * atlasWidth;
                for (int x = 0; x < CellSize; x++)
                {
                    Color color = spritePixels[sourceRow + x];
                    if (tintHitFlash && color.A > 0)
                    {
                        color = Color.FromArgb(color.A, 255, Math.Min(255, color.G + 40), Math.Min(255, color.B + 40));
                    }

                    atlasPixels[atlasRow + atlasX + x] = color.ToArgb();
                }
            }

            // 셀 바깥 1픽셀에 가장자리 색을 복제해 두면 확대/회전 중에도
            // 이웃 atlas 슬롯 색이 섞이지 않아 스프라이트 깨짐이 줄어든다.
            for (int y = 0; y < CellSize; y++)
            {
                int row = atlasY + y;
                int baseIndex = row * atlasWidth;
                atlasPixels[baseIndex + atlasCellX] = atlasPixels[baseIndex + atlasX];
                atlasPixels[baseIndex + atlasX + CellSize] = atlasPixels[baseIndex + atlasX + CellSize - 1];
            }

            int topRow = atlasY * atlasWidth;
            int bottomRow = (atlasY + CellSize - 1) * atlasWidth;
            int paddingTopRow = atlasCellY * atlasWidth;
            int paddingBottomRow = (atlasY + CellSize) * atlasWidth;
            for (int x = 0; x < CellSize; x++)
            {
                atlasPixels[paddingTopRow + atlasX + x] = atlasPixels[topRow + atlasX + x];
                atlasPixels[paddingBottomRow + atlasX + x] = atlasPixels[bottomRow + atlasX + x];
            }

            atlasPixels[(atlasCellY * atlasWidth) + atlasCellX] = atlasPixels[(atlasY * atlasWidth) + atlasX];
            atlasPixels[(atlasCellY * atlasWidth) + atlasX + CellSize] = atlasPixels[(atlasY * atlasWidth) + atlasX + CellSize - 1];
            atlasPixels[((atlasY + CellSize) * atlasWidth) + atlasCellX] = atlasPixels[((atlasY + CellSize - 1) * atlasWidth) + atlasX];
            atlasPixels[((atlasY + CellSize) * atlasWidth) + atlasX + CellSize] = atlasPixels[((atlasY + CellSize - 1) * atlasWidth) + atlasX + CellSize - 1];
        }
    }

    /// <summary>
    /// 참조 동일성(ReferenceEquals)만 비교하는 IEqualityComparer 구현.
    /// SpriteAtlasCache의 슬롯 맵 딕셔너리에서 오브젝트 참조를 키로 사용할 때 필요하다.
    /// </summary>
    internal sealed class ReferenceComparer : IEqualityComparer<object>
    {
        /// <summary>싱글톤 인스턴스.</summary>
        public static readonly ReferenceComparer Instance = new();

        /// <summary>
        /// 두 오브젝트 참조가 동일한지(같은 메모리 주소) 확인한다.
        /// </summary>
        /// <param name="x">비교할 첫 번째 오브젝트.</param>
        /// <param name="y">비교할 두 번째 오브젝트.</param>
        /// <returns>두 참조가 동일하면 true.</returns>
        public new bool Equals(object x, object y)
        {
            return ReferenceEquals(x, y);
        }

        /// <summary>
        /// RuntimeHelpers.GetHashCode를 사용하여 참조 기반 해시를 반환한다.
        /// </summary>
        /// <param name="obj">해시 코드를 계산할 오브젝트.</param>
        /// <returns>참조 기반 해시 코드.</returns>
        public int GetHashCode(object obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
        }
    }
}
