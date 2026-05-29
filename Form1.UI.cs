using System;
using System.Drawing;
using My2DEngine.Engine.Rendering;
namespace My2DEngine
{
    /// <summary>
    /// Form1의 UI 렌더링 partial 클래스.
    /// 메인 메뉴, 모드 선택, 설정, 기록, 일시정지/사망 오버레이의 레이아웃 계산과 드로우를 담당한다.
    /// </summary>
    public partial class Form1
    {
        /// <summary>UI 레이아웃 기준 해상도 너비. 모든 메뉴/오버레이 좌표는 이 크기에서 1.0배가 되도록 설계한다.</summary>
        private const float UiBaseWidth = 1280f;

        /// <summary>UI 레이아웃 기준 해상도 높이.</summary>
        private const float UiBaseHeight = 720f;

        /// <summary>
        /// 현재 클라이언트 크기에 대한 균일 UI 배율을 반환한다.
        /// 기준 해상도(1280x720) 대비 가로/세로 중 작은 비율을 사용해 종횡비가 달라도 왜곡 없이
        /// 비례하며, 극단적으로 작거나 큰 창에서도 가독성을 유지하도록 클램프한다.
        /// 메뉴/설정/기록/사망/일시정지 화면의 폰트·패널·버튼 크기에 곱해 사용한다.
        /// </summary>
        private float GetUiScale(int width, int height)
        {
            float s = Math.Min(width / UiBaseWidth, height / UiBaseHeight);
            if (s < 0.55f) s = 0.55f;
            if (s > 1.8f) s = 1.8f;
            return s;
        }

        /// <summary>
        /// 창 크기가 변경된 경우에만 모든 UI 레이아웃을 재계산한다.
        /// 이전과 동일한 크기이면 캐시된 값을 재사용하여 불필요한 계산을 생략한다.
        /// </summary>
        /// <param name="width">현재 클라이언트 영역 너비.</param>
        /// <param name="height">현재 클라이언트 영역 높이.</param>
        private void EnsureUiLayout(int width, int height)
        {
            if (width == cachedLayoutWidth && height == cachedLayoutHeight)
            {
                return;
            }

            cachedLayoutWidth = width;
            cachedLayoutHeight = height;
            UpdateMenuLayout(width, height);
            UpdateModeLayout(width, height);
            UpdatePauseLayout(width, height);
            UpdateDeathLayout(width, height);
            UpdateSettingsLayout(width, height);
        }

        /// <summary>
        /// 메인 메뉴 화면을 그린다. 배경, 로고(또는 텍스트 타이틀), 부제목,
        /// 게임 시작/설정/종료 버튼을 순서대로 렌더링한다.
        /// 마우스 커서 위치를 기반으로 버튼 hover 효과를 적용한다.
        /// </summary>
        /// <param name="r">드로우 명령을 받을 렌더러 인스턴스.</param>
        /// <param name="width">클라이언트 영역 너비 (레이아웃 기준).</param>
        /// <param name="height">클라이언트 영역 높이 (레이아웃 기준).</param>
        private void DrawMenu(Renderer r, int width, int height)
        {
            DrawSharedMenuBackground(r, width, height);

            float s = GetUiScale(width, height);

            // 엠블럼 로고가 있으면 상단에 작게 배치하고, 그 아래에 텍스트 타이틀을 함께 그린다.
            float titleY;
            if (menuLogo != null)
            {
                float maxLogoW = Math.Min(width * 0.5f, 360f * s);
                float maxLogoH = Math.Min(height * 0.20f, 190f * s);
                float scale = Math.Min(maxLogoW / menuLogo.Width, maxLogoH / menuLogo.Height);
                float logoW = menuLogo.Width * scale;
                float logoH = menuLogo.Height * scale;
                float logoX = (width - logoW) * 0.5f;
                float logoY = height * 0.07f;
                r.DrawImage(menuLogo, logoX, logoY, logoW, logoH);
                titleY = logoY + logoH + 30f * s;
            }
            else
            {
                titleY = height * 0.28f;
            }

            // 텍스트 타이틀(붉은 글로우)은 로고 유무와 관계없이 항상 표시한다.
            float titleSize = 44f * s;
            DrawGlowText(r, "KILLING FIELD", width * 0.5f, titleY, Color.FromArgb(45, 210, 60, 48), titleSize, 3.2f * s);
            r.DrawTextCenteredShadow("KILLING FIELD", width * 0.5f, titleY, Color.FromArgb(245, 205, 60, 54), titleSize);
            // 타이틀 아래 짧은 붉은 구분선
            r.DrawRectangle(width * 0.5f - 150f * s, titleY + 36f * s, 300f * s, 2f * s, Color.FromArgb(190, 175, 42, 38));

            // 부제는 타이틀 아래에 상대 배치해 로고 유무에 따라 겹치지 않게 한다.
            float subY = titleY + 64f * s;
            r.DrawTextCenteredShadow("ABANDONED SECTOR SURVIVAL", width * 0.5f, subY, Color.FromArgb(225, 215, 215, 215), 14f * s);
            // 부제 좌우의 가는 장식 라인 (텍스트와 충분히 떨어뜨려 겹침 방지)
            r.DrawRectangle(width * 0.5f - 250f * s, subY + 7f * s, 60f * s, 1f, Color.FromArgb(110, 180, 180, 180));
            r.DrawRectangle(width * 0.5f + 190f * s, subY + 7f * s, 60f * s, 1f, Color.FromArgb(110, 180, 180, 180));

            DrawMenuActionButton(r, startButtonRect, "게임 시작", "모드 선택 후 전장 진입", Color.FromArgb(210, 130, 36, 36), startButtonRect.Contains(lastMousePosition));
            DrawMenuActionButton(r, settingsButtonRect, "설정", "시야각과 마우스 감도 조정", Color.FromArgb(210, 92, 96, 104), settingsButtonRect.Contains(lastMousePosition));
            DrawMenuActionButton(r, recordsButtonRect, "기록", "역대 도전 기록 보기", Color.FromArgb(210, 60, 90, 130), recordsButtonRect.Contains(lastMousePosition));
            DrawMenuActionButton(r, exitButtonRect, "게임 종료", "세션 종료", Color.FromArgb(210, 88, 52, 52), exitButtonRect.Contains(lastMousePosition));

            r.DrawText("[I]  영구 스탯 보기", 24f * s, height * 0.93f, Color.FromArgb(180, 190, 190, 190), 11f * s);
        }

        /// <summary>
        /// 모드 선택 화면을 그린다. 배경, 패널, 제목, 설명 텍스트,
        /// 일반 모드/무한 모드/뒤로 버튼을 렌더링한다.
        /// </summary>
        /// <param name="r">드로우 명령을 받을 렌더러 인스턴스.</param>
        /// <param name="width">클라이언트 영역 너비.</param>
        /// <param name="height">클라이언트 영역 높이.</param>
        private void DrawModeSelect(Renderer r, int width, int height)
        {
            DrawSharedMenuBackground(r, width, height);

            float s = GetUiScale(width, height);
            int panelW = (int)(600 * s);
            int panelH = GetModePanelHeight(s);
            int panelX = (width - panelW) / 2;
            int panelY = (height - panelH) / 2;

            DrawUiPanel(r, panelX, panelY, panelW, panelH, Color.FromArgb(220, 24, 24, 24), 30f * s);
            if (uiPanelFrame == null)
            {
                r.DrawRectangle(panelX + 10 * s, panelY + 10 * s, panelW - 20 * s, 6 * s, Color.FromArgb(220, 140, 34, 34));
            }
            r.DrawTextCenteredShadow("진행 모드 선택", width * 0.5f, panelY + 48 * s, Color.White, 26f * s);
            r.DrawTextCenteredShadow("전투 밸런스는 기본값으로 고정됩니다", width * 0.5f, panelY + 86 * s, Color.Gainsboro, 14f * s);

            DrawMenuActionButton(r, modeNormalRect, "일반 모드", "1층부터 로그라이크 런 시작", Color.FromArgb(210, 150, 118, 48), modeNormalRect.Contains(lastMousePosition));

            if (CanStartEndlessRun)
            {
                DrawMenuActionButton(r, modeEndlessRect, "무한 모드", "667층부터 시작하는 해금 콘텐츠", Color.FromArgb(210, 70, 118, 162), modeEndlessRect.Contains(lastMousePosition));
            }
            else
            {
                float eh = modeEndlessRect.Height;
                r.DrawRectangle(modeEndlessRect.X, modeEndlessRect.Y, modeEndlessRect.Width, modeEndlessRect.Height, Color.FromArgb(200, 34, 34, 34));
                r.DrawRectangle(modeEndlessRect.X + eh * 0.12f, modeEndlessRect.Y + eh * 0.12f, eh * 0.12f, modeEndlessRect.Height - eh * 0.24f, Color.FromArgb(140, 90, 90, 90));
                r.DrawText("무한 모드", modeEndlessRect.X + eh * 0.53f, modeEndlessRect.Y + eh * 0.18f, Color.FromArgb(170, 205, 205, 205), eh * 0.25f);
                r.DrawText("666층 클리어 시 해금", modeEndlessRect.X + eh * 0.53f, modeEndlessRect.Y + eh * 0.56f, Color.FromArgb(170, 165, 165, 165), eh * 0.155f);
            }

            if (CanContinueSavedRun)
            {
                DrawMenuActionButton(r, continueButtonRect, "이어하기", "저장된 런 이어서 진행", Color.FromArgb(210, 46, 110, 62), continueButtonRect.Contains(lastMousePosition));
            }

            DrawMenuButton(r, modeBackRect, "뒤로", modeBackRect.Contains(lastMousePosition) ? Color.FromArgb(225, 74, 74, 74) : Color.FromArgb(210, 42, 42, 42));
        }

        /// <summary>
        /// 게임 화면 위에 반투명 배경을 씌운 일시정지 오버레이를 그린다.
        /// "게임 계속", "설정", "메인메뉴" 버튼이 포함된다.
        /// </summary>
        /// <param name="r">드로우 명령을 받을 렌더러 인스턴스.</param>
        /// <param name="width">클라이언트 영역 너비.</param>
        /// <param name="height">클라이언트 영역 높이.</param>
        private void DrawPauseOverlay(Renderer r, int width, int height)
        {
            r.DrawRectangle(0, 0, width, height, Color.FromArgb(160, 0, 0, 0));

            float s = GetUiScale(width, height);
            // 패널 크기는 UpdatePauseLayout과 같은 상수를 사용해 버튼과 정렬을 맞춘다.
            int panelW = (int)(PausePanelWidth * s);
            int panelH = (int)(PausePanelHeight * s);
            int panelX = (width - panelW) / 2;
            int panelY = (height - panelH) / 2;

            DrawUiPanel(r, panelX, panelY, panelW, panelH, Color.FromArgb(220, 30, 30, 30), 30f * s);
            if (uiPanelFrame == null)
            {
                r.DrawRectangle(panelX + 10 * s, panelY + 10 * s, panelW - 20 * s, 6 * s, Color.FromArgb(220, 140, 34, 34));
            }

            r.DrawTextCenteredShadow("일시정지", width * 0.5f, panelY + 52 * s, Color.White, 24f * s);
            DrawMenuButton(r, pauseResumeRect, "게임 계속");
            DrawMenuButton(r, pauseSettingsRect, "설정");
            DrawMenuButton(r, pauseMenuRect, "메인메뉴");
        }

        /// <summary>
        /// 사망 후 게임 월드 위에 런 요약과 다음 행동 버튼을 그린다.
        /// 런 요약은 GameLogic의 종료 시점 고정 스냅샷을 사용해 화면에 머문 시간이 플레이 시간에 섞이지 않게 한다.
        /// </summary>
        /// <param name="r">드로우 명령을 받을 렌더러 인스턴스.</param>
        /// <param name="width">클라이언트 영역 너비.</param>
        /// <param name="height">클라이언트 영역 높이.</param>
        private void DrawDeathOverlay(Renderer r, int width, int height)
        {
            r.DrawRectangle(0, 0, width, height, Color.FromArgb(185, 0, 0, 0));

            float s = GetUiScale(width, height);
            int panelW = (int)(540 * s);
            int panelH = (int)(DeathPanelHeight * s);
            int panelX = (width - panelW) / 2;
            int panelY = (height - panelH) / 2;
            float cx = width * 0.5f;

            r.DrawRectangle(panelX, panelY, panelW, panelH, Color.FromArgb(236, 24, 20, 20));
            r.DrawRectangle(panelX, panelY, panelW, 4 * s, Color.FromArgb(230, 160, 38, 38));
            r.DrawRectangle(panelX + 24 * s, panelY + 86 * s, panelW - 48 * s, 1, Color.FromArgb(100, 255, 255, 255));

            r.DrawTextCenteredShadow("사망", cx, panelY + 34 * s, Color.FromArgb(255, 255, 222, 222), 26f * s);
            r.DrawTextCenteredShadow("런 요약", cx, panelY + 72 * s, Color.FromArgb(230, 230, 230, 230), 12f * s);

            WorldRunSummarySnapshot summary = CaptureWorldRunSummarySnapshot();
            int rowX = panelX + (int)(58 * s);
            int valueX = panelX + panelW - (int)(58 * s);
            int rowY = panelY + (int)(116 * s);
            int rowGap = (int)(34 * s);
            DrawDeathSummaryRow(r, "도달 층", $"{summary.FloorReached}층", rowX, valueX, rowY, s);
            DrawDeathSummaryRow(r, "처치한 적", $"{summary.EnemiesKilled}명", rowX, valueX, rowY + rowGap, s);
            DrawDeathSummaryRow(r, "처치한 보스", $"{summary.BossesKilled}명", rowX, valueX, rowY + rowGap * 2, s);
            DrawDeathSummaryRow(r, "플레이 시간", FormatDuration(summary.DurationSeconds), rowX, valueX, rowY + rowGap * 3, s);

            DrawDeathCardSummary(r, summary.AcquiredCards, panelX, panelW, rowY + rowGap * 4 + (int)(6 * s), s);

            DrawMenuButton(
                r,
                deathRestartRect,
                "재시작",
                deathRestartRect.Contains(lastMousePosition)
                    ? Color.FromArgb(230, 130, 48, 42)
                    : Color.FromArgb(215, 92, 36, 34));
            DrawMenuButton(
                r,
                deathMenuRect,
                "메인메뉴",
                deathMenuRect.Contains(lastMousePosition)
                    ? Color.FromArgb(230, 70, 84, 108)
                    : Color.FromArgb(215, 44, 54, 74));
        }

        /// <summary>
        /// 사망 화면의 요약 라벨/값 한 줄을 같은 열 위치에 맞춰 그린다.
        /// </summary>
        private void DrawDeathSummaryRow(Renderer r, string label, string value, int labelX, int valueX, int y, float s)
        {
            r.DrawText(label, labelX, y, Color.FromArgb(205, 188, 188, 188), 12f * s);
            r.DrawText(value, valueX - 120 * s, y, Color.FromArgb(245, 245, 245, 245), 12f * s);
        }

        /// <summary>
        /// 사망 화면 하단에 이번 런에서 획득한 스탯 카드 목록을 2열로 그린다.
        /// 표시 한도를 넘으면 남은 종류 수를 요약 표시한다.
        /// </summary>
        private void DrawDeathCardSummary(Renderer r, string[] cards, int panelX, int panelW, int startY, float s)
        {
            float cx = panelX + panelW * 0.5f;
            r.DrawRectangle(panelX + 24 * s, startY, panelW - 48 * s, 1, Color.FromArgb(80, 255, 255, 255));
            r.DrawTextCenteredShadow("획득한 카드", cx, startY + 14 * s, Color.FromArgb(220, 255, 220, 160), 11f * s);

            if (cards == null || cards.Length == 0)
            {
                r.DrawTextCenteredShadow("획득한 카드 없음", cx, startY + 44 * s, Color.FromArgb(170, 180, 180, 180), 10f * s);
                return;
            }

            const int maxRows = 4;
            int maxShown = maxRows * 2;
            int shown = Math.Min(cards.Length, maxShown);
            float colLeftX = panelX + 44 * s;
            float colRightX = panelX + panelW / 2 + 10 * s;
            float listY = startY + 34 * s;
            float lineH = 18 * s;

            for (int i = 0; i < shown; i++)
            {
                float x = (i % 2 == 0) ? colLeftX : colRightX;
                float y = listY + (i / 2) * lineH;
                r.DrawText("· " + cards[i], x, y, Color.FromArgb(230, 225, 225, 225), 9f * s);
            }

            if (cards.Length > shown)
            {
                r.DrawTextCenteredShadow($"+{cards.Length - shown}종 더", cx, listY + maxRows * lineH,
                    Color.FromArgb(180, 200, 200, 200), 9f * s);
            }
        }

        /// <summary>
        /// 설정 화면을 그린다. 시야각 슬라이더, 마우스 감도 슬라이더,
        /// 창 크기 선택 버튼, 뒤로 버튼이 포함된다.
        /// 일시정지 중 열린 설정이면 게임 화면 위에 반투명 오버레이로 표시된다.
        /// </summary>
        /// <param name="r">드로우 명령을 받을 렌더러 인스턴스.</param>
        /// <param name="width">클라이언트 영역 너비.</param>
        /// <param name="height">클라이언트 영역 높이.</param>
        /// <param name="overlay">
        /// true이면 반투명 배경 오버레이로 그리고,
        /// false이면 불투명 단색 배경으로 그린다.
        /// </param>
        private void DrawSettings(Renderer r, int width, int height, bool overlay)
        {
            WorldSettingsSnapshot settings = GetDisplayedSettings();

            if (overlay)
            {
                // 일시정지에서 연 설정은 게임 화면 위 반투명 오버레이로 유지한다.
                r.DrawRectangle(0, 0, width, height, Color.FromArgb(160, 0, 0, 0));
            }
            else
            {
                // 메뉴에서 연 독립 설정 화면은 다른 메뉴 화면과 같은 공통 배경을 사용한다.
                DrawSharedMenuBackground(r, width, height);
            }

            float s = GetUiScale(width, height);
            int panelW = (int)(620 * s);
            // 패널 높이는 UpdateSettingsLayout과 동일하게 590 기준이어야 모든 슬라이더/버튼을 감싼다.
            int panelH = (int)(590 * s);
            int panelX = (width - panelW) / 2;
            int panelY = (height - panelH) / 2;
            DrawUiPanel(r, panelX, panelY, panelW, panelH, Color.FromArgb(220, 30, 30, 30), 30f * s);

            r.DrawTextCenteredShadow("설정", width * 0.5f, panelY + 38 * s, Color.White, 24f * s);

            float fovValue = settings.FovDegrees;
            float sensValue = settings.MouseSensitivity;
            float labelOffY = 32 * s;
            float valueOffX = 14 * s;
            float valueOffY = 8 * s;
            float labelSize = 14f * s;

            // 시야각 슬라이더
            r.DrawText("시야각 (60 - 90)", fovSliderRect.X, fovSliderRect.Y - labelOffY, Color.White, labelSize);
            DrawSlider(r, fovSliderRect, GetRatio(fovValue, FovMin, FovMax));
            r.DrawText(fovValue.ToString("0"), fovSliderRect.Right + valueOffX, fovSliderRect.Y - valueOffY, Color.White, labelSize);

            // 감도변경 슬라이더
            r.DrawText("마우스 감도", sensSliderRect.X, sensSliderRect.Y - labelOffY, Color.White, labelSize);
            DrawSlider(r, sensSliderRect, GetRatio(sensValue, SensMin, SensMax));
            r.DrawText(sensValue.ToString("0.000"), sensSliderRect.Right + valueOffX, sensSliderRect.Y - valueOffY, Color.White, labelSize);

            // 창크기 변경 박스: 960, 1270, 1600, 1920
            r.DrawText("창 크기 (16:9 고정)", resolutionPrevRect.X, resolutionPrevRect.Y - 34 * s, Color.White, labelSize);
            DrawMenuButton(r, resolutionPrevRect, "<", resolutionPrevRect.Contains(lastMousePosition) ? Color.FromArgb(225, 74, 74, 74) : Color.FromArgb(210, 42, 42, 42));
            DrawMenuButton(r, resolutionNextRect, ">", resolutionNextRect.Contains(lastMousePosition) ? Color.FromArgb(225, 74, 74, 74) : Color.FromArgb(210, 42, 42, 42));
            r.DrawRectangle(resolutionPrevRect.Right + 14 * s, resolutionPrevRect.Y, resolutionNextRect.X - resolutionPrevRect.Right - 28 * s, resolutionPrevRect.Height, Color.FromArgb(210, 38, 38, 38));
            r.DrawTextCentered(GetCurrentWindowSizeLabel(), width * 0.5f, resolutionPrevRect.Y + resolutionPrevRect.Height * 0.5f, Color.White, 18f * s);

            // BGM 볼륨 슬라이더: 0~100 범위를 0.0~1.0 비율로 변환하여 표시한다.
            int bgmVolume = settings.BgmVolume;
            r.DrawText("BGM 볼륨", bgmSliderRect.X, bgmSliderRect.Y - labelOffY, Color.White, labelSize);
            DrawSlider(r, bgmSliderRect, bgmVolume / 100f);
            r.DrawText(bgmVolume.ToString(), bgmSliderRect.Right + valueOffX, bgmSliderRect.Y - valueOffY, Color.White, labelSize);

            // 효과음 볼륨 슬라이더: 0~100 범위를 0.0~1.0 비율로 변환하여 표시한다.
            int sfxVolume = settings.SfxVolume;
            r.DrawText("효과음 볼륨", sfxSliderRect.X, sfxSliderRect.Y - labelOffY, Color.White, labelSize);
            DrawSlider(r, sfxSliderRect, sfxVolume / 100f);
            r.DrawText(sfxVolume.ToString(), sfxSliderRect.Right + valueOffX, sfxSliderRect.Y - valueOffY, Color.White, labelSize);

            DrawMenuButton(r, backButtonRect, "뒤로");
        }

        /// <summary>
        /// 메뉴 화면의 배경 장식을 그린다.
        /// 상단/하단 색띠, 수평/수직 구분선 등 전술적 분위기의 기하학적 요소들로 구성된다.
        /// </summary>
        /// <param name="r">드로우 명령을 받을 렌더러 인스턴스.</param>
        /// <param name="width">클라이언트 영역 너비.</param>
        /// <param name="height">클라이언트 영역 높이.</param>
        /// <summary>
        /// 메인 메뉴 배경 일러스트를 화면 비율에 맞게 cover 방식(비율 유지·넘침 크롭)으로 그린 뒤,
        /// 텍스트 가독성을 위한 어둠 스크림과 상·하단 그라데이션을 덧씌운다.
        /// 이미지 영역 밖으로 넘친 부분은 GPU 래스터라이저가 자동으로 클리핑한다.
        /// </summary>
        /// <summary>
        /// 메뉴 계열 독립 화면(메인 메뉴·모드 선택·기록·설정)의 공통 배경을 그린다.
        /// 배경 일러스트가 있으면 이미지+스크림을, 없으면 절차적 배경을 사용해 화면 간 톤을 통일한다.
        /// 게임 위에 겹쳐 그리는 일시정지·사망 오버레이에는 사용하지 않는다.
        /// </summary>
        private void DrawSharedMenuBackground(Renderer r, int width, int height)
        {
            if (menuBackground != null)
            {
                DrawMenuBackgroundImage(r, width, height);
            }
            else
            {
                DrawMenuBackdrop(r, width, height);
            }
        }

        private void DrawMenuBackgroundImage(Renderer r, int width, int height)
        {
            // 바탕은 일단 검정으로 채워, 이미지 종횡비가 화면과 달라도 빈틈이 비치지 않게 한다.
            r.DrawRectangle(0, 0, width, height, Color.FromArgb(255, 6, 6, 8));

            float iw = menuBackground.Width;
            float ih = menuBackground.Height;
            float scale = Math.Max(width / iw, height / ih);
            float dw = iw * scale;
            float dh = ih * scale;
            float dx = (width - dw) * 0.5f;
            float dy = (height - dh) * 0.5f;
            r.DrawImage(menuBackground, dx, dy, dw, dh);

            // 전체 약한 어둠 + 하단(버튼 영역 가독성) 그라데이션 스크림.
            // 상단 붉은 그라데이션은 제거해 배경 일러스트가 그대로 드러나게 한다.
            r.DrawRectangle(0, 0, width, height, Color.FromArgb(70, 8, 6, 10));

            int bands = 14;
            float bandTop = height * 0.42f;
            float bandH = (height - bandTop) / bands;
            for (int i = 0; i < bands; i++)
            {
                int a = (int)(8 + (i / (float)(bands - 1)) * 165);
                r.DrawRectangle(0, bandTop + i * bandH, width, bandH + 1f, Color.FromArgb(a, 5, 4, 8));
            }
        }

        /// <summary>
        /// 패널 배경을 그린다. 9-slice 패널 프레임 텍스처가 있으면 그것을, 없으면 단색 사각형을 사용한다.
        /// </summary>
        /// <param name="dstBorder">패널 프레임 모서리가 차지할 두께(내부 렌더 해상도 기준 픽셀).</param>
        private void DrawUiPanel(Renderer r, float x, float y, float w, float h, Color fallback, float dstBorder)
        {
            if (uiPanelFrame != null)
            {
                r.DrawImageNineSlice(uiPanelFrame, x, y, w, h, uiPanelFrame.Width * 0.16f, dstBorder);
            }
            else
            {
                r.DrawRectangle(x, y, w, h, fallback);
            }
        }

        /// <summary>
        /// 같은 텍스트를 반투명 색으로 여러 방향에 겹쳐 그려 글로우(블룸) 느낌을 낸다.
        /// 본 텍스트를 그리기 직전에 호출한다.
        /// </summary>
        private void DrawGlowText(Renderer r, string text, float cx, float cy, Color glow, float size, float spread)
        {
            float d = spread;
            float h = spread * 0.7f;
            float[] ox = { -d, d, 0f, 0f, -h, h, -h, h };
            float[] oy = { 0f, 0f, -d, d, -h, -h, h, h };
            for (int i = 0; i < ox.Length; i++)
            {
                r.DrawTextCentered(text, cx + ox[i], cy + oy[i], glow, size);
            }
        }

        private void DrawMenuBackdrop(Renderer r, int width, int height)
        {
            r.DrawRectangle(0, 0, width, height, Color.FromArgb(18, 18, 18));
            r.DrawRectangle(0, 0, width, height * 0.22f, Color.FromArgb(255, 32, 18, 18));
            r.DrawRectangle(0, height * 0.78f, width, height * 0.22f, Color.FromArgb(255, 20, 20, 20));
            r.DrawRectangle(width * 0.08f, height * 0.12f, width * 0.84f, 3, Color.FromArgb(180, 120, 28, 28));
            r.DrawRectangle(width * 0.16f, height * 0.84f, width * 0.68f, 2, Color.FromArgb(120, 180, 180, 180));
            r.DrawRectangle(width * 0.1f, height * 0.16f, 6, height * 0.62f, Color.FromArgb(70, 120, 28, 28));
            r.DrawRectangle(width * 0.9f, height * 0.16f, 6, height * 0.62f, Color.FromArgb(70, 120, 28, 28));
        }

        /// <summary>
        /// 제목과 부제목을 가진 액션 버튼을 그린다.
        /// hover 상태이면 강조 테두리, 더 밝은 패널 색, 더 큰 텍스트로 표시된다.
        /// 왼쪽에 accent 색띠가 세로로 그려진다.
        /// </summary>
        /// <param name="r">드로우 명령을 받을 렌더러 인스턴스.</param>
        /// <param name="rect">버튼이 차지할 화면 영역.</param>
        /// <param name="title">버튼의 주 제목 텍스트 (예: "게임 시작").</param>
        /// <param name="subtitle">버튼의 부제목 텍스트 (예: "모드 선택 후 전장 진입").</param>
        /// <param name="accentColor">왼쪽 색띠와 hover 테두리에 사용할 강조 색.</param>
        /// <param name="highlighted">마우스 커서가 버튼 위에 있을 때 true.</param>
        private void DrawMenuActionButton(Renderer r, Rectangle rect, string title, string subtitle, Color accentColor, bool highlighted)
        {
            float h = rect.Height;

            // 호버 시 버튼 외곽에 강조 글로우 테두리를 두른다(버튼 높이 비례).
            if (highlighted)
            {
                float border = h * 0.07f;
                r.DrawRectangle(rect.X - border, rect.Y - border, rect.Width + border * 2f, rect.Height + border * 2f, Color.FromArgb(95, accentColor.R, accentColor.G, accentColor.B));
            }

            // 배경 일러스트가 살짝 비치도록 반투명 패널을 쓰되, 텍스트 가독성은 유지한다.
            float pad = h * 0.12f;
            float accentWidth = highlighted ? h * 0.15f : h * 0.12f;
            float textX = rect.X + h * 0.53f;
            float titleSize = (highlighted ? 0.265f : 0.25f) * h;
            float subtitleSize = (highlighted ? 0.162f : 0.155f) * h;

            // 배경: 9-slice 프레임 텍스처가 있으면 그것을, 없으면 단색 패널을 사용한다.
            if (uiButtonFrame != null)
            {
                Image frame = (highlighted && uiButtonFrameHover != null) ? uiButtonFrameHover : uiButtonFrame;
                // 테두리 두께는 가로·세로 중 작은 쪽 기준으로 잡아, 얇은 버튼에서 가운데가 사라지지 않게 한다.
                float dstBorder = Math.Min(rect.Width, rect.Height) * 0.40f;
                r.DrawImageNineSlice(frame, rect.X, rect.Y, rect.Width, rect.Height, frame.Width * 0.16f, dstBorder);
            }
            else
            {
                Color panelColor = highlighted ? Color.FromArgb(235, 46, 46, 50) : Color.FromArgb(212, 28, 28, 32);
                r.DrawRectangle(rect.X, rect.Y, rect.Width, rect.Height, panelColor);
                // 상단 하이라이트 라인 + 하단 음영 라인으로 입체감을 준다.
                r.DrawRectangle(rect.X, rect.Y, rect.Width, Math.Max(1f, h * 0.03f), Color.FromArgb(highlighted ? 120 : 70, 255, 255, 255));
                r.DrawRectangle(rect.X, rect.Y + rect.Height - Math.Max(1f, h * 0.03f), rect.Width, Math.Max(1f, h * 0.03f), Color.FromArgb(120, 0, 0, 0));
                r.DrawRectangle(rect.X + h * 0.35f, rect.Y + pad, rect.Width - h * 0.47f, 1, Color.FromArgb(highlighted ? 110 : 70, 255, 255, 255));
            }
            // 왼쪽 accent 색띠 (프레임/단색 공통, 버튼 종류 구분용)
            r.DrawRectangle(rect.X + pad, rect.Y + pad, accentWidth, rect.Height - pad * 2f, accentColor);
            r.DrawText(title, textX, rect.Y + h * 0.18f, Color.White, titleSize);
            r.DrawText(subtitle, textX, rect.Y + h * 0.56f, Color.FromArgb(220, 210, 210, 210), subtitleSize);

            // 호버 시 우측에 진입 화살표 마커를 표시한다.
            if (highlighted)
            {
                r.DrawText("▶", rect.X + rect.Width - h * 0.62f, rect.Y + h * 0.32f, accentColor, h * 0.3f);
            }
        }

        /// <summary>
        /// 기본 어두운 배경색으로 단순 버튼을 그린다.
        /// </summary>
        /// <param name="r">드로우 명령을 받을 렌더러 인스턴스.</param>
        /// <param name="rect">버튼이 차지할 화면 영역.</param>
        /// <param name="text">버튼 중앙에 표시할 텍스트.</param>
        private void DrawMenuButton(Renderer r, Rectangle rect, string text)
        {
            DrawMenuButton(r, rect, text, Color.FromArgb(200, 40, 40, 40));
        }

        /// <summary>
        /// 지정한 배경색으로 단순 버튼을 그린다. 텍스트는 버튼 중앙에 흰색으로 표시된다.
        /// </summary>
        /// <param name="r">드로우 명령을 받을 렌더러 인스턴스.</param>
        /// <param name="rect">버튼이 차지할 화면 영역.</param>
        /// <param name="text">버튼 중앙에 표시할 텍스트.</param>
        /// <param name="color">버튼 배경색.</param>
        private void DrawMenuButton(Renderer r, Rectangle rect, string text, Color color)
        {
            // 9-slice 버튼 프레임이 있으면 그것을, 없으면 단색을 사용한다.
            // hover는 커서가 버튼 위에 있는지로 내부 판정해 호출부 변경 없이 강조 프레임을 쓴다.
            if (uiButtonFrame != null)
            {
                bool hover = rect.Contains(lastMousePosition);
                Image frame = (hover && uiButtonFrameHover != null) ? uiButtonFrameHover : uiButtonFrame;
                float dstBorder = Math.Min(rect.Width, rect.Height) * 0.40f;
                r.DrawImageNineSlice(frame, rect.X, rect.Y, rect.Width, rect.Height, frame.Width * 0.16f, dstBorder);
            }
            else
            {
                r.DrawRectangle(rect.X, rect.Y, rect.Width, rect.Height, color);
            }
            // 폰트를 버튼 높이에 비례시켜 rect만 스케일해도 텍스트가 잘리지 않게 한다.
            float fontSize = Math.Min(rect.Height * 0.38f, rect.Width * 0.5f);
            r.DrawTextCentered(text, rect.X + rect.Width * 0.5f, rect.Y + rect.Height * 0.5f, Color.White, fontSize);
        }

        /// <summary>
        /// 가로 슬라이더를 그린다. 배경 트랙, 채워진 부분, 드래그 손잡이(knob)로 구성된다.
        /// ratio가 0이면 완전히 비어있고, 1이면 완전히 채워진다.
        /// </summary>
        /// <param name="r">드로우 명령을 받을 렌더러 인스턴스.</param>
        /// <param name="rect">슬라이더 트랙이 차지할 화면 영역.</param>
        /// <param name="ratio">현재 값의 비율 (0.0 ~ 1.0). 자동으로 클램핑된다.</param>
        private void DrawSlider(Renderer r, Rectangle rect, float ratio)
        {
            ratio = Clamp01(ratio);
            r.DrawRectangle(rect.X, rect.Y, rect.Width, rect.Height, Color.FromArgb(120, 60, 60, 60));

            int filledW = (int)(rect.Width * ratio);
            if (filledW > 0)
            {
                r.DrawRectangle(rect.X, rect.Y, filledW, rect.Height, Color.FromArgb(200, 90, 170, 90));
            }

            // 손잡이 크기를 트랙 높이에 비례시켜 슬라이더 rect 스케일에 자동으로 맞춘다.
            int knobW = Math.Max(8, (int)(rect.Height * 1.33f));
            int knobH = Math.Max(16, (int)(rect.Height * 2.33f));
            int knobX = rect.X + filledW - knobW / 2;
            if (knobX < rect.X - knobW / 2) knobX = rect.X - knobW / 2;
            if (knobX > rect.X + rect.Width - knobW / 2) knobX = rect.X + rect.Width - knobW / 2;
            int knobY = rect.Y - (knobH - rect.Height) / 2;
            r.DrawRectangle(knobX, knobY, knobW, knobH, Color.FromArgb(220, 220, 220, 220));
        }

        /// <summary>
        /// 메인 메뉴 버튼들의 위치와 크기를 현재 창 크기에 맞게 계산한다.
        /// 버튼은 수직 방향으로 일정 간격을 두고 화면 중앙에 정렬된다.
        /// </summary>
        /// <param name="width">클라이언트 영역 너비.</param>
        /// <param name="height">클라이언트 영역 높이.</param>
        private void UpdateMenuLayout(int width, int height)
        {
            float s = GetUiScale(width, height);
            int buttonW = (int)(360 * s);
            int buttonH = (int)(68 * s);
            int centerX = width / 2;
            int spacing = (int)(18 * s);

            int startY = (int)(height * 0.50f);
            startButtonRect    = new Rectangle(centerX - buttonW / 2, startY,                          buttonW, buttonH);
            settingsButtonRect = new Rectangle(centerX - buttonW / 2, startY + (buttonH + spacing),     buttonW, buttonH);
            recordsButtonRect  = new Rectangle(centerX - buttonW / 2, startY + (buttonH + spacing) * 2, buttonW, buttonH);
            exitButtonRect     = new Rectangle(centerX - buttonW / 2, startY + (buttonH + spacing) * 3, buttonW, buttonH);
        }

        /// <summary>
        /// 모드 선택 화면 버튼들의 위치와 크기를 현재 창 크기에 맞게 계산한다.
        /// 패널 중앙을 기준으로 일반 모드/무한 모드/뒤로 버튼의 영역을 설정한다.
        /// </summary>
        /// <param name="width">클라이언트 영역 너비.</param>
        /// <param name="height">클라이언트 영역 높이.</param>
        private void UpdateModeLayout(int width, int height)
        {
            float s = GetUiScale(width, height);
            int panelH = GetModePanelHeight(s);
            int panelY = (height - panelH) / 2;

            int buttonW = (int)(340 * s);
            int buttonH = (int)(60 * s);
            int spacing = (int)(14 * s);
            int centerX = width / 2;
            int startY = panelY + (int)(126 * s);

            modeNormalRect  = new Rectangle(centerX - buttonW / 2, startY,                          buttonW, buttonH);
            modeEndlessRect = new Rectangle(centerX - buttonW / 2, startY + (buttonH + spacing),     buttonW, buttonH);
            continueButtonRect = new Rectangle(centerX - buttonW / 2, startY + (buttonH + spacing) * 2, buttonW, buttonH);
            modeBackRect    = new Rectangle(centerX - (int)(96 * s), panelY + panelH - (int)(62 * s), (int)(192 * s), (int)(46 * s));
        }

        /// <summary>
        /// 이어하기 버튼 표시 여부에 따라 모드 선택 패널 높이를 조정한다.
        /// </summary>
        private int GetModePanelHeight(float s)
        {
            return (int)((CanContinueSavedRun ? 490 : 420) * s);
        }

        /// <summary>
        /// 일시정지 화면 버튼들의 위치와 크기를 현재 창 크기에 맞게 계산한다.
        /// 게임 계속/설정/메인메뉴 버튼이 화면 중앙에 수직으로 배치된다.
        /// </summary>
        /// <param name="width">클라이언트 영역 너비.</param>
        /// <param name="height">클라이언트 영역 높이.</param>
        /// <summary>일시정지 패널의 기준 너비/높이(스케일 적용 전). DrawPauseOverlay와 공유한다.</summary>
        private const float PausePanelWidth = 380f;
        private const float PausePanelHeight = 340f;

        private void UpdatePauseLayout(int width, int height)
        {
            float s = GetUiScale(width, height);
            float panelH = PausePanelHeight * s;
            float panelY = (height - panelH) * 0.5f;

            int buttonW = (int)(250 * s);
            int buttonH = (int)(52 * s);
            int centerX = width / 2;
            int startY = (int)(panelY + 108 * s);
            int spacing = (int)(16 * s);

            pauseResumeRect = new Rectangle(centerX - buttonW / 2, startY, buttonW, buttonH);
            pauseSettingsRect = new Rectangle(centerX - buttonW / 2, startY + buttonH + spacing, buttonW, buttonH);
            pauseMenuRect = new Rectangle(centerX - buttonW / 2, startY + (buttonH + spacing) * 2, buttonW, buttonH);
        }

        /// <summary>
        /// 사망 오버레이의 버튼 영역을 현재 창 크기에 맞춰 계산한다.
        /// 버튼 위치는 DrawDeathOverlay의 패널 계산과 같은 식을 사용해야 클릭 영역과 렌더 위치가 어긋나지 않는다.
        /// </summary>
        /// <param name="width">클라이언트 영역 너비.</param>
        /// <param name="height">클라이언트 영역 높이.</param>
        /// <summary>사망 오버레이 패널 높이. UpdateDeathLayout과 DrawDeathOverlay가 공유한다.</summary>
        private const int DeathPanelHeight = 460;

        private void UpdateDeathLayout(int width, int height)
        {
            float s = GetUiScale(width, height);
            int panelW = (int)(540 * s);
            int panelH = (int)(DeathPanelHeight * s);
            int panelX = (width - panelW) / 2;
            int panelY = (height - panelH) / 2;
            int buttonH = (int)(50 * s);
            int buttonW = (int)(210 * s);
            int buttonY = panelY + panelH - (int)(76 * s);

            deathRestartRect = new Rectangle(panelX + (int)(28 * s), buttonY, buttonW, buttonH);
            deathMenuRect = new Rectangle(panelX + panelW - (int)(28 * s) - buttonW, buttonY, buttonW, buttonH);
        }

        /// <summary>
        /// 설정 화면 컨트롤들의 위치와 크기를 현재 창 크기에 맞게 계산한다.
        /// FOV 슬라이더, 감도 슬라이더, 창 크기 이전/다음 버튼, 뒤로 버튼의 영역을 설정한다.
        /// </summary>
        /// <param name="width">클라이언트 영역 너비.</param>
        /// <param name="height">클라이언트 영역 높이.</param>
        private void UpdateSettingsLayout(int width, int height)
        {
            float s = GetUiScale(width, height);
            int panelW = (int)(620 * s);
            int panelH = (int)(590 * s);
            int panelX = (width - panelW) / 2;
            int panelY = (height - panelH) / 2;

            int sliderW = panelW - (int)(180 * s);
            int sliderH = Math.Max(8, (int)(12 * s));
            int sliderX = panelX + (int)(36 * s);
            int firstY = panelY + (int)(112 * s);
            int spacing = (int)(84 * s);

            fovSliderRect = new Rectangle(sliderX, firstY, sliderW, sliderH);
            sensSliderRect = new Rectangle(sliderX, firstY + spacing, sliderW, sliderH);

            int resolutionY = sensSliderRect.Y + spacing;
            int resolutionButtonW = (int)(54 * s);
            int resolutionButtonH = (int)(42 * s);
            resolutionPrevRect = new Rectangle(panelX + (int)(36 * s), resolutionY, resolutionButtonW, resolutionButtonH);
            resolutionNextRect = new Rectangle(panelX + panelW - (int)(36 * s) - resolutionButtonW, resolutionY, resolutionButtonW, resolutionButtonH);

            int bgmY = resolutionY + resolutionButtonH + (int)(42 * s);
            bgmSliderRect = new Rectangle(sliderX, bgmY, sliderW, sliderH);
            sfxSliderRect = new Rectangle(sliderX, bgmY + spacing, sliderW, sliderH);

            backButtonRect = new Rectangle(panelX + panelW / 2 - (int)(96 * s), panelY + panelH - (int)(72 * s), (int)(192 * s), (int)(46 * s));
        }

        /// <summary>
        /// 메인 메뉴 화면에서의 클릭을 처리한다.
        /// 클릭 위치에 따라 모드 선택 화면 열기, 설정 화면 열기, 또는 게임 종료를 수행한다.
        /// </summary>
        /// <param name="location">클릭한 클라이언트 좌표.</param>
        private void HandleMenuClick(Point location)
        {
            EnsureUiLayout(ClientSize.Width, ClientSize.Height);

            if (startButtonRect.Contains(location))
            {
                OpenModeSelect();
                return;
            }

            if (settingsButtonRect.Contains(location))
            {
                OpenSettingsFromMenu();
                return;
            }

            if (recordsButtonRect.Contains(location))
            {
                EnterMenuState(GameState.Records);
                return;
            }

            if (exitButtonRect.Contains(location))
            {
                Close();
            }
        }

        /// <summary>
        /// 모드 선택 화면에서의 클릭을 처리한다.
        /// 일반 모드 또는 무한 모드를 시작하거나, 뒤로 버튼을 클릭하면 메인 메뉴로 돌아간다.
        /// </summary>
        /// <param name="location">클릭한 클라이언트 좌표.</param>
        private void HandleModeClick(Point location)
        {
            UpdateModeLayout(ClientSize.Width, ClientSize.Height);

            if (modeNormalRect.Contains(location))
            {
                StartNormalGame();
                return;
            }

            if (CanStartEndlessRun && modeEndlessRect.Contains(location))
            {
                StartEndlessGame();
                return;
            }

            if (CanContinueSavedRun && continueButtonRect.Contains(location))
            {
                ContinueGame();
                return;
            }

            if (modeBackRect.Contains(location))
            {
                EnterMenuState(GameState.Menu);
            }
        }

        /// <summary>
        /// 일시정지 화면에서의 클릭을 처리한다.
        /// 게임 계속, 설정 열기, 메인 메뉴로 이동 중 하나를 수행한다.
        /// </summary>
        /// <param name="location">클릭한 클라이언트 좌표.</param>
        private void HandlePauseClick(Point location)
        {
            EnsureUiLayout(ClientSize.Width, ClientSize.Height);

            if (pauseResumeRect.Contains(location))
            {
                ResumeGame();
                return;
            }

            if (pauseSettingsRect.Contains(location))
            {
                OpenSettingsFromPause();
                return;
            }

            if (pauseMenuRect.Contains(location))
            {
                GoToMenu();
            }
        }

        /// <summary>
        /// 사망 오버레이 클릭을 처리한다.
        /// 버튼 밖 클릭도 소비해 사망 상태에서 월드 발사 입력으로 떨어지지 않게 한다.
        /// </summary>
        /// <param name="location">클릭한 클라이언트 좌표.</param>
        /// <returns>사망 오버레이가 클릭을 소비했으면 true.</returns>
        private bool HandleDeathClick(Point location)
        {
            EnsureUiLayout(ClientSize.Width, ClientSize.Height);

            if (deathRestartRect.Contains(location))
            {
                RestartDeadRun();
                return true;
            }

            if (deathMenuRect.Contains(location))
            {
                GoToMenu();
                return true;
            }

            return true;
        }

        /// <summary>
        /// 설정 화면에서의 클릭을 처리한다.
        /// 뒤로 버튼, FOV 슬라이더, 감도 슬라이더, 창 크기 이전/다음 버튼 클릭을 구분하여 처리한다.
        /// 슬라이더 클릭 시 드래그 상태를 시작하고 즉시 값을 적용한다.
        /// </summary>
        /// <param name="location">클릭한 클라이언트 좌표.</param>
        private void HandleSettingsClick(Point location)
        {
            EnsureUiLayout(ClientSize.Width, ClientSize.Height);

            if (backButtonRect.Contains(location))
            {
                ExitSettings();
                return;
            }

            if (fovSliderRect.Contains(location))
            {
                draggingFov = true;
                SetFovFromMouse(location.X);
            }
            else if (sensSliderRect.Contains(location))
            {
                draggingSensitivity = true;
                SetSensitivityFromMouse(location.X);
            }
            else if (bgmSliderRect.Contains(location))
            {
                draggingBgm = true;
                SetBgmVolumeFromMouse(location.X);
            }
            else if (sfxSliderRect.Contains(location))
            {
                draggingSfx = true;
                SetSfxVolumeFromMouse(location.X);
            }
            else if (resolutionPrevRect.Contains(location))
            {
                ChangeWindowSizePreset(-1);
            }
            else if (resolutionNextRect.Contains(location))
            {
                ChangeWindowSizePreset(1);
            }
        }

        /// <summary>
        /// 마우스 X 좌표를 FOV 슬라이더 범위에 매핑하여 시야각을 설정한다.
        /// 슬라이더 영역 밖의 좌표는 자동으로 클램핑된다.
        /// </summary>
        /// <param name="mouseX">마우스의 클라이언트 X 좌표.</param>
        private void SetFovFromMouse(int mouseX)
        {
            float t = (mouseX - fovSliderRect.X) / (float)fovSliderRect.Width;
            t = Clamp01(t);
            float value = FovMin + (t * (FovMax - FovMin));
            pendingSettings = pendingSettings with { FovDegrees = value };
            MarkSettingsDirty();
        }

        /// <summary>
        /// 마우스 X 좌표를 감도 슬라이더 범위에 매핑하여 마우스 감도를 설정한다.
        /// 슬라이더 영역 밖의 좌표는 자동으로 클램핑된다.
        /// </summary>
        /// <param name="mouseX">마우스의 클라이언트 X 좌표.</param>
        private void SetSensitivityFromMouse(int mouseX)
        {
            float t = (mouseX - sensSliderRect.X) / (float)sensSliderRect.Width;
            t = Clamp01(t);
            float value = SensMin + (t * (SensMax - SensMin));
            pendingSettings = pendingSettings with { MouseSensitivity = value };
            MarkSettingsDirty();
        }

        /// <summary>
        /// 마우스 X 좌표를 BGM 볼륨 슬라이더 범위에 매핑하여 BGM 볼륨을 설정한다.
        /// 슬라이더 영역 밖의 좌표는 자동으로 클램핑된다.
        /// </summary>
        /// <param name="mouseX">마우스의 클라이언트 X 좌표.</param>
        private void SetBgmVolumeFromMouse(int mouseX)
        {
            float t = (mouseX - bgmSliderRect.X) / (float)bgmSliderRect.Width;
            t = Clamp01(t);
            int volume = (int)Math.Round(t * 100f);
            pendingSettings = pendingSettings with { BgmVolume = volume };
            MarkSettingsDirty();
        }

        /// <summary>
        /// 마우스 X 좌표를 효과음 볼륨 슬라이더 범위에 매핑하여 효과음 볼륨을 설정한다.
        /// 슬라이더 영역 밖의 좌표는 자동으로 클램핑된다.
        /// </summary>
        /// <param name="mouseX">마우스의 클라이언트 X 좌표.</param>
        private void SetSfxVolumeFromMouse(int mouseX)
        {
            float t = (mouseX - sfxSliderRect.X) / (float)sfxSliderRect.Width;
            t = Clamp01(t);
            int volume = (int)Math.Round(t * 100f);
            pendingSettings = pendingSettings with { SfxVolume = volume };
            MarkSettingsDirty();
        }

        /// <summary>
        /// value를 [min, max] 범위 내에서 0 ~ 1 비율로 정규화하여 반환한다.
        /// 슬라이더의 현재 값을 드로우 비율로 변환할 때 사용된다.
        /// </summary>
        /// <param name="value">정규화할 현재 값.</param>
        /// <param name="min">범위 최솟값.</param>
        /// <param name="max">범위 최댓값.</param>
        /// <returns>0.0 ~ 1.0 범위의 정규화된 비율. max &lt;= min이면 0을 반환한다.</returns>
        private float GetRatio(float value, float min, float max)
        {
            if (max <= min)
            {
                return 0f;
            }

            return (value - min) / (max - min);
        }

        /// <summary>
        /// value를 0.0 ~ 1.0 범위로 클램핑하여 반환한다.
        /// 슬라이더 비율이 범위를 벗어나지 않도록 보정할 때 사용된다.
        /// </summary>
        /// <param name="value">클램핑할 값.</param>
        /// <returns>0.0 미만이면 0.0, 1.0 초과이면 1.0, 그 외에는 원래 값.</returns>
        private float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }

        private void DrawRecords(Renderer r, int width, int height)
        {
            DrawSharedMenuBackground(r, width, height);

            float s = GetUiScale(width, height);
            float cx = width * 0.5f;
            float panelW = 740f * s;
            float panelH = 520f * s;
            float panelX = cx - panelW * 0.5f;
            float panelY = (height - panelH) * 0.5f;

            DrawUiPanel(r, panelX, panelY, panelW, panelH, Color.FromArgb(228, 14, 18, 28), 30f * s);
            if (uiPanelFrame == null)
            {
                r.DrawRectangle(panelX, panelY, panelW, 1f, Color.FromArgb(200, 100, 140, 200));
                r.DrawRectangle(panelX, panelY + panelH - 1f, panelW, 1f, Color.FromArgb(200, 100, 140, 200));
                r.DrawRectangle(panelX, panelY, 1f, panelH, Color.FromArgb(200, 100, 140, 200));
                r.DrawRectangle(panelX + panelW - 1f, panelY, 1f, panelH, Color.FromArgb(200, 100, 140, 200));
            }

            r.DrawTextCenteredShadow("[ 기록 ]", cx, panelY + 20f * s, Color.FromArgb(255, 180, 210, 255), 16f * s);

            WorldRunRecordSnapshot[] records = LoadWorldRunRecords(15);

            // 정렬 적용: 최신순(0)은 DB 기본 순서를 유지하고, 그 외는 기준값 내림차순으로 정렬한다.
            if (recordsSortMode != 0 && records.Length > 1)
            {
                Array.Sort(records, (a, b) =>
                {
                    switch (recordsSortMode)
                    {
                        case 1: return b.FloorReached.CompareTo(a.FloorReached);
                        case 2: return b.EnemiesKilled.CompareTo(a.EnemiesKilled);
                        case 3: return b.DurationSeconds.CompareTo(a.DurationSeconds);
                        default: return 0;
                    }
                });
            }

            if (records.Length == 0)
            {
                recordsSortRect = Rectangle.Empty;
                r.DrawTextCenteredShadow("아직 도전 기록이 없습니다.", cx, panelY + panelH * 0.5f, Color.FromArgb(200, 180, 180, 180), 13f * s);
            }
            else
            {
                int sortBtnW = (int)(132 * s), sortBtnH = (int)(26 * s);
                recordsSortRect = new Rectangle((int)(panelX + panelW - sortBtnW - 16f * s), (int)(panelY + 12f * s), sortBtnW, sortBtnH);
                Color sortColor = recordsSortRect.Contains(lastMousePosition)
                    ? Color.FromArgb(220, 60, 80, 110)
                    : Color.FromArgb(200, 36, 50, 72);
                DrawMenuButton(r, recordsSortRect, "정렬: " + GetRecordsSortLabel(), sortColor);

                int bestFloor = 0, bestKills = 0, victoryCount = 0;
                foreach (var rec in records)
                {
                    if (rec.FloorReached > bestFloor) bestFloor = rec.FloorReached;
                    if (rec.EnemiesKilled > bestKills) bestKills = rec.EnemiesKilled;
                    if (rec.IsVictory) victoryCount++;
                }

                string summaryText = $"총 {records.Length}회 도전  |  최고 {bestFloor}층  |  최고 처치 {bestKills}명";
                if (victoryCount > 0)
                    summaryText += $"  |  클리어 {victoryCount}회";
                r.DrawTextCenteredShadow(summaryText, cx, panelY + 48f * s,
                    victoryCount > 0 ? Color.FromArgb(230, 130, 230, 140) : Color.FromArgb(220, 200, 200, 200), 9.5f * s);

                float headerY = panelY + 72f * s;
                float colFlag   = panelX + panelW * 0.04f;
                float colFloor  = panelX + panelW * 0.12f;
                float colEnemy  = panelX + panelW * 0.30f;
                float colBoss   = panelX + panelW * 0.48f;
                float colTime   = panelX + panelW * 0.65f;
                float colDate   = panelX + panelW * 0.84f;
                Color headerCol = Color.FromArgb(255, 180, 210, 255);
                Color dimLine   = Color.FromArgb(80, 180, 210, 255);
                float headerSize = 9f * s;
                float rowSize = 9.5f * s;
                float dateSize = 9f * s;

                r.DrawText("도달 층", colFloor, headerY, headerCol, headerSize);
                r.DrawText("적 처치", colEnemy, headerY, headerCol, headerSize);
                r.DrawText("보스 처치", colBoss, headerY, headerCol, headerSize);
                r.DrawText("플레이 시간", colTime, headerY, headerCol, headerSize);
                r.DrawText("날짜", colDate, headerY, headerCol, headerSize);

                float rowH = 28f * s;
                float rowStart = headerY + 22f * s;
                for (int i = 0; i < records.Length; i++)
                {
                    var rec = records[i];
                    float rowY = rowStart + i * rowH;
                    if (rowY + rowH > panelY + panelH - 58f * s) break;

                    if (i % 2 == 0)
                        r.DrawRectangle(panelX + 2f, rowY - 4f * s, panelW - 4f, rowH, Color.FromArgb(40, 0, 0, 0));

                    r.DrawRectangle(panelX + 2f, rowY + rowH - 5f * s, panelW - 4f, 1f, dimLine);

                    Color rowCol = i == 0 ? Color.FromArgb(255, 255, 200, 60) : Color.FromArgb(255, 220, 220, 220);
                    string timeStr = FormatDuration(rec.DurationSeconds);
                    if (rec.IsVictory)
                        r.DrawText("★", colFlag, rowY, Color.FromArgb(255, 100, 240, 130), rowSize);
                    r.DrawText($"{rec.FloorReached}층", colFloor, rowY, rowCol, rowSize);
                    r.DrawText($"{rec.EnemiesKilled}명", colEnemy, rowY, rowCol, rowSize);
                    r.DrawText($"{rec.BossesKilled}명", colBoss, rowY, rowCol, rowSize);
                    r.DrawText(timeStr, colTime, rowY, rowCol, rowSize);
                    r.DrawText(rec.EndedAt, colDate, rowY, rowCol, dateSize);
                }
            }

            int backW = (int)(180 * s), backH = (int)(44 * s);
            recordsBackRect = new Rectangle((int)(cx - backW * 0.5f), (int)(panelY + panelH - 58f * s), backW, backH);
            Color backColor = recordsBackRect.Contains(lastMousePosition)
                ? Color.FromArgb(220, 60, 80, 110)
                : Color.FromArgb(200, 36, 50, 72);
            DrawMenuButton(r, recordsBackRect, "← 뒤로", backColor);

            r.DrawTextCenteredShadow("ESC 또는 클릭으로 뒤로", cx, panelY + panelH - 10f * s, Color.FromArgb(150, 180, 180, 180), 8f * s);
        }

        private void HandleRecordsClick(Point location)
        {
            if (recordsSortRect != Rectangle.Empty && recordsSortRect.Contains(location))
            {
                // 최신 → 도달 층 → 적 처치 → 플레이 시간 순으로 순환한다.
                recordsSortMode = (recordsSortMode + 1) % 4;
                return;
            }

            if (recordsBackRect.Contains(location))
                EnterMenuState(GameState.Menu);
        }

        /// <summary>현재 기록 정렬 기준의 표시용 라벨을 반환한다.</summary>
        private string GetRecordsSortLabel()
        {
            switch (recordsSortMode)
            {
                case 1: return "도달 층";
                case 2: return "적 처치";
                case 3: return "플레이 시간";
                default: return "최신";
            }
        }

        private static string FormatDuration(int seconds)
        {
            int m = seconds / 60;
            int s = seconds % 60;
            return m > 0 ? $"{m}분 {s:00}초" : $"{s}초";
        }
    }
}
