using System;
using System.Drawing;
using My2DEngine.Engine.Rendering;
using My2DEngine.Game.Config;
using My2DEngine.Game.Map;
using My2DEngine.Game.Rendering.Ui;
using My2DEngine.Game.Systems;
using My2DEngine.Game;

namespace My2DEngine.Game.Rendering
{
    /// <summary>
    /// 체력 바, 스태미나 바, 탄약 카운터, 미니맵, 보스 HUD, 무기 오버레이,
    /// 피격 오버레이, 사망 연출 등 모든 화면 HUD와 오버레이를 담당하는 partial 클래스.
    /// 이미지 없이 <see cref="PixelUi"/>로 640x360 내부 좌표에 픽셀 한 칸(u=1) 단위로 그린다.
    /// 그리기는 배경 패스(…Backdrop, 게이지, 미니맵)와 글자 패스(…Hud, …Overlay, 카운터) 두 단계로 나뉜다.
    /// </summary>
    public partial class RaycastRenderer
    {
        /// <summary>HUD 픽셀 한 칸 크기(내부 좌표).</summary>
        private const float HudUnit = 1f;

        /// <summary>HUD 작은 글자 크기(내부 좌표). 1280x720에서 12px.</summary>
        private const float HudSmallText = PixelUi.FontBase * 0.5f;

        /// <summary>HUD 큰 숫자 크기(내부 좌표). 1280x720에서 24px.</summary>
        private const float HudLargeText = PixelUi.FontBase;

        /// <summary>미니맵에서 플레이어 중심 기준으로 표시할 타일 반경(타일 단위).</summary>
        private const int MiniMapTileRadius = 5;

        /// <summary>미니맵에서 타일 하나를 표시하는 픽셀 크기.</summary>
        private const int MiniMapCellSize = 8;

        /// <summary>미니맵 상자의 화면 가장자리 여백.</summary>
        private const float MiniMapMargin = 8f;

        // === 좌하단 상태 상자(보호막·체력·스태미나) ===
        private const float StatusBoxW = 172f;
        private const float StatusBoxH = 54f;
        private const float StatusBoxMargin = 8f;

        // === 우하단 상자(탄약 아래, 코인 위) ===
        private const float AmmoBoxW = 132f;
        private const float AmmoBoxH = 48f;
        private const float CoinBoxH = 20f;
        private const float RightBoxMargin = 8f;
        private const float RightBoxGap = 3f;

        /// <summary>깜박임용 시간(초).</summary>
        private static float HudTime => Environment.TickCount64 / 1000f;

        private void GetStatusBoxRect(out float x, out float y, out float w, out float h)
        {
            w = StatusBoxW;
            h = StatusBoxH;
            x = StatusBoxMargin;
            y = frameH - StatusBoxMargin - StatusBoxH;
        }

        private void GetAmmoBoxRect(out float x, out float y, out float w, out float h)
        {
            w = AmmoBoxW;
            h = AmmoBoxH;
            x = frameW - RightBoxMargin - AmmoBoxW;
            y = frameH - RightBoxMargin - AmmoBoxH;
        }

        private void GetCoinBoxRect(out float x, out float y, out float w, out float h)
        {
            GetAmmoBoxRect(out x, out float ammoY, out w, out _);
            h = CoinBoxH;
            y = ammoY - RightBoxGap - CoinBoxH;
        }

        /// <summary>상태 상자 안 slot(0=보호막, 1=체력, 2=스태미나) 줄의 라벨·막대·값 위치.</summary>
        private void GetStatusRow(int slot, out float labelX, out float barX, out float barY, out float barW, out float barH, out float valueRight)
        {
            GetStatusBoxRect(out float x, out float y, out float w, out _);
            labelX = x + 6f;
            barX = x + 22f;
            valueRight = x + w - 6f;
            barW = w - 22f - 30f;
            barH = slot == 2 ? 8f : 12f;
            barY = y + 5f + slot * 15f + (slot == 2 ? 2f : 0f);
        }

        /// <summary>
        /// 보스 이름을 보스 체력 상자 위에 그린다(글자 패스).
        /// </summary>
        /// <param name="r">텍스트를 그릴 렌더러.</param>
        /// <param name="bossEnemy">보스 적 인스턴스. null이거나 사망 상태이면 무시된다.</param>
        private void DrawBossHud(Renderer r, Enemy bossEnemy)
        {
            if (bossEnemy == null || !bossEnemy.Alive)
            {
                return;
            }

            GetBossBarRect(out float x, out float y, out float w, out _);
            string name = string.IsNullOrWhiteSpace(bossEnemy.DisplayName) ? "Boss" : bossEnemy.DisplayName;
            PixelUi.Text(r, name, x + 6f, y + 4f, PixelPalette.Brass, HudSmallText, HudUnit, bold: true);
            int pct = bossEnemy.MaxHealth > 0f ? (int)Math.Ceiling(100f * bossEnemy.Health / bossEnemy.MaxHealth) : 0;
            PixelUi.TextRight(r, pct + "%", x + w - 6f, y + 4f, PixelPalette.Text, HudSmallText, HudUnit);
        }

        /// <summary>보스 체력 상자 위치.</summary>
        private void GetBossBarRect(out float x, out float y, out float w, out float h)
        {
            w = Math.Min(frameW * 0.46f, 296f);
            h = 30f;
            x = (frameW - w) * 0.5f;
            y = 6f;
        }

        /// <summary>
        /// 스테이지 상태 메시지, 상호작용 힌트, 보스 등장 안내, 스테이지 클리어 문구를 그린다(글자 패스).
        /// 상자는 <see cref="DrawStageBackdrop"/>가 먼저 그린다.
        /// </summary>
        /// <param name="r">텍스트를 그릴 렌더러.</param>
        /// <param name="stageStatusMessage">화면 상단에 표시할 스테이지 상태 메시지.</param>
        /// <param name="stageStatusAlpha">상태 메시지 불투명도(0~1). 만료 직전에 줄어든다.</param>
        /// <param name="interactPromptText">화면 하단에 표시할 상호작용 힌트 텍스트.</param>
        /// <param name="bossIntroTimer">보스 등장 연출 남은 시간(초). 0 이하이면 생략한다.</param>
        /// <param name="bossEnemy">보스 적 인스턴스. 이름 표시에 사용된다.</param>
        /// <param name="victory">스테이지 클리어 여부.</param>
        private void DrawStageOverlay(Renderer r, string stageStatusMessage, float stageStatusAlpha, string interactPromptText, float bossIntroTimer, Enemy bossEnemy, bool victory)
        {
            if (!string.IsNullOrWhiteSpace(stageStatusMessage) && stageStatusAlpha > 0f)
            {
                float a = Math.Min(1f, stageStatusAlpha);
                GetStageMessageRect(r, stageStatusMessage, out float x, out float y, out float w, out float h);
                PixelUi.TextCentered(r, stageStatusMessage, x + w * 0.5f, y + h * 0.5f, PixelUi.Fade(PixelPalette.Text, a), HudSmallText, HudUnit);
            }

            if (!string.IsNullOrWhiteSpace(interactPromptText))
            {
                GetInteractPromptRect(r, interactPromptText, out float x, out float y, out float w, out float h);
                PixelUi.TextCentered(r, interactPromptText, x + w * 0.5f, y + h * 0.5f, PixelPalette.Brass, HudSmallText, HudUnit, bold: true);
            }

            if (bossIntroTimer > 0f && bossEnemy != null && bossEnemy.Alive)
            {
                float a = Math.Min(1f, bossIntroTimer / EnemyConfig.BossIntroDuration);
                GetBossIntroRect(out float x, out float y, out float w, out float h);
                PixelUi.TextCentered(r, "BOSS ENCOUNTER", x + w * 0.5f, y + h * 0.38f, PixelUi.Fade(PixelPalette.AccentHot, a), HudLargeText, HudUnit, bold: true);
                PixelUi.TextCentered(r, string.IsNullOrWhiteSpace(bossEnemy.DisplayName) ? "Arena Warden" : bossEnemy.DisplayName,
                    x + w * 0.5f, y + h * 0.72f, PixelUi.Fade(PixelPalette.Text, a), HudSmallText, HudUnit);
            }

            if (victory)
            {
                GetVictoryRect(out float x, out float y, out float w, out float h);
                PixelUi.TextCentered(r, "STAGE CLEAR", x + w * 0.5f, y + h * 0.38f, PixelPalette.Brass, HudLargeText, HudUnit, bold: true);
                PixelUi.TextCentered(r, "Boss eliminated", x + w * 0.5f, y + h * 0.72f, PixelPalette.Text, HudSmallText, HudUnit);
            }
        }

        /// <summary>
        /// "YOU DIED" 문구를 그린다. deathProgress가 0.2 미만이면 표시하지 않는다.
        /// </summary>
        /// <param name="r">텍스트를 그릴 렌더러.</param>
        /// <param name="deathProgress">사망 연출 진행 비율(0=시작, 1=완료).</param>
        private void DrawDeathPresentation(Renderer r, float deathProgress)
        {
            float uiFade = deathProgress <= 0.2f ? 0f : (deathProgress - 0.2f) / 0.8f;
            uiFade = EaseOutCubic(uiFade);
            if (uiFade <= 0f)
            {
                return;
            }

            GetDeathPanelRect(out float x, out float y, out float w, out float h);
            PixelUi.TextCentered(r, "YOU DIED", x + w * 0.5f, y + h * 0.42f, PixelUi.Fade(PixelPalette.Accent, uiFade), HudLargeText * 2f, HudUnit, bold: true);
            PixelUi.TextCentered(r, "Run terminated", x + w * 0.5f, y + h * 0.78f, PixelUi.Fade(PixelPalette.TextDim, uiFade), HudSmallText, HudUnit);
        }

        private void GetDeathPanelRect(out float x, out float y, out float w, out float h)
        {
            w = Math.Min(frameW * 0.5f, 300f);
            h = 64f;
            x = (frameW - w) * 0.5f;
            y = frameH * 0.30f;
        }

        /// <summary>
        /// 피격 방향에 따라 화면 흔들림 오프셋을 계산한다.
        /// 피격 방향 벡터를 플레이어 시야 좌표계로 분해한 뒤 사인/코사인 진동을 더해
        /// 방향성 있는 충격감 있는 흔들림을 생성한다.
        /// </summary>
        /// <param name="player">시야 방향 계산 기준이 되는 플레이어 상태.</param>
        /// <param name="shakeTimer">피격 흔들림 남은 시간(초). 0 이하이면 오프셋이 0이다.</param>
        /// <param name="shakePower">흔들림 세기(0~1).</param>
        /// <param name="damageDirX">피격 방향 벡터의 X 성분(월드 공간).</param>
        /// <param name="damageDirY">피격 방향 벡터의 Y 성분(월드 공간).</param>
        /// <param name="offsetX">계산된 수평 화면 오프셋(픽셀).</param>
        /// <param name="offsetY">계산된 수직 화면 오프셋(픽셀).</param>
        private void GetDamageShakeOffset(Player player, float shakeTimer, float shakePower, float damageDirX, float damageDirY,
            out float offsetX, out float offsetY)
        {
            offsetX = 0f;
            offsetY = 0f;

            if (shakeTimer <= 0f || shakePower <= 0f)
            {
                return;
            }

            float dirLen = (float)Math.Sqrt((damageDirX * damageDirX) + (damageDirY * damageDirY));
            if (dirLen <= 0.001f)
            {
                damageDirX = -player.Direction.X;
                damageDirY = -player.Direction.Y;
                dirLen = 1f;
            }

            damageDirX /= dirLen;
            damageDirY /= dirLen;

            float rightX = -player.Direction.Y;
            float rightY = player.Direction.X;
            float dirForward = (damageDirX * player.Direction.X) + (damageDirY * player.Direction.Y);
            float dirRight = (damageDirX * rightX) + (damageDirY * rightY);

            float amplitude = (3f + 7f * shakePower) * Math.Min(1f, shakeTimer / 0.28f);
            float oscillationX = (float)Math.Sin(shakeTimer * 75f);
            float oscillationY = (float)Math.Cos(shakeTimer * 62f);

            offsetX = ((oscillationX * 0.55f) - (dirRight * 0.95f)) * amplitude;
            offsetY = ((oscillationY * 0.4f) - (dirForward * 0.7f)) * amplitude;
        }

        /// <summary>
        /// 무기 발사 반동에 따른 화면 흔들림 오프셋을 계산한다.
        /// 수직 킥과 가로 진동을 합산하여 사실적인 총기 반동 느낌을 연출한다.
        /// </summary>
        /// <param name="shakeTimer">반동 흔들림 남은 시간(초). 0 이하이면 오프셋이 0이다.</param>
        /// <param name="shakePower">반동 세기(0~1).</param>
        /// <param name="offsetX">계산된 수평 화면 오프셋(픽셀).</param>
        /// <param name="offsetY">계산된 수직 화면 오프셋(픽셀).</param>
        private void GetRecoilShakeOffset(float shakeTimer, float shakePower, out float offsetX, out float offsetY)
        {
            offsetX = 0f;
            offsetY = 0f;

            if (shakeTimer <= 0f || shakePower <= 0f)
            {
                return;
            }

            float intensity = Math.Min(1f, shakeTimer / 0.18f) * shakePower;
            float lateral = (float)Math.Sin(shakeTimer * 108f) * (2.2f + shakePower * 2.6f);
            float verticalKick = (5f + shakePower * 8f) * intensity;
            float settle = (float)Math.Cos(shakeTimer * 56f) * 1.4f * intensity;

            offsetX = lateral * intensity;
            offsetY = -verticalKick + settle;
        }

        /// <summary>
        /// 탄약 상자 안의 무기 이름, 탄약 수, 특수기 상태를 그린다(글자 패스).
        /// </summary>
        /// <param name="r">텍스트를 그릴 렌더러.</param>
        /// <param name="weapon">탄약 및 특수기 상태를 제공하는 무기 인스턴스.</param>
        private void DrawAmmoCounter(Renderer r, Weapon weapon)
        {
            if (r == null || weapon == null)
            {
                return;
            }

            GetAmmoBoxRect(out float x, out float y, out float w, out float h);
            PixelUi.Text(r, GetHudWeaponName(weapon.CurrentType), x + 6f, y + 5f, PixelPalette.TextDim, HudSmallText, HudUnit);

            bool empty = weapon.CurrentAmmo <= 0;
            bool low = weapon.MagazineSize > 0 && weapon.CurrentAmmo <= weapon.MagazineSize * 0.2f;
            Color ammoColor = empty
                ? PixelPalette.Accent
                : low && ((int)(HudTime * 4f) % 2 == 0) ? PixelPalette.AccentHot : PixelPalette.Text;
            PixelUi.TextRight(r, weapon.CurrentAmmo.ToString("00"), x + w - 34f, y + 17f, ammoColor, HudLargeText, HudUnit, bold: true);
            PixelUi.Text(r, "/" + weapon.MagazineSize.ToString("00"), x + w - 32f, y + 23f, PixelPalette.TextDim, HudSmallText, HudUnit);

            string specialStatus = GetSpecialStatusText(weapon);
            if (!string.IsNullOrWhiteSpace(specialStatus))
            {
                Color statusColor = specialStatus == "F READY" ? PixelPalette.Good : PixelPalette.Brass;
                PixelUi.Text(r, specialStatus, x + 6f, y + h - 13f, statusColor, HudSmallText, HudUnit);
            }
        }

        private string GetHudWeaponName(WeaponType type)
        {
            return WeaponPresentation.GetHudName(type);
        }

        private string GetSpecialStatusText(Weapon weapon)
        {
            if (weapon == null || !weapon.HasSpecialUpgrade(weapon.CurrentType))
            {
                return null;
            }

            if ((weapon.CurrentType == WeaponType.HChainGun || weapon.CurrentType == WeaponType.DuelBerettas) &&
                weapon.SpecialUsedThisFloor)
            {
                return "F USED";
            }

            if (weapon.SpecialCooldownTimer > 0f)
            {
                return "F " + weapon.SpecialCooldownTimer.ToString("0.0") + "s";
            }

            return "F READY";
        }

        /// <summary>
        /// 코인 상자 안의 라벨과 보유 코인 수를 그린다(글자 패스).
        /// </summary>
        /// <param name="r">텍스트를 그릴 렌더러.</param>
        /// <param name="player">코인 상태를 제공하는 플레이어 상태.</param>
        private void DrawCoinHud(Renderer r, Player player)
        {
            if (r == null || player == null)
            {
                return;
            }

            GetCoinBoxRect(out float x, out float y, out float w, out float h);
            float textY = y + (h - HudSmallText) * 0.5f - 1f;
            PixelUi.Text(r, "COIN", x + 14f, textY, PixelPalette.Brass, HudSmallText, HudUnit, bold: true);
            PixelUi.TextRight(r, player.CoinCount.ToString(), x + w - 6f, textY, PixelPalette.Text, HudSmallText, HudUnit, bold: true);
        }

        /// <summary>
        /// 타일 타입 값에 따라 미니맵에 표시할 색상을 반환한다.
        /// 빈 공간, 문, 특수 타일, 벽 타일이 각각 다른 색상으로 구분된다.
        /// </summary>
        /// <param name="tileType">맵 배열의 타일 타입 값.</param>
        /// <returns>미니맵에 표시할 ARGB 색상.</returns>
        private Color GetMiniMapTileColor(int tileType)
        {
            switch (tileType)
            {
                case 0:
                    return Color.FromArgb(255, 26, 22, 26);
                case WorldConfig.DoorTileType:
                    return PixelPalette.Brass;
                case 6:
                    return Color.FromArgb(255, 70, 128, 72);
                case 7:
                    return Color.FromArgb(255, 150, 98, 48);
                case 8:
                    return Color.FromArgb(255, 70, 102, 150);
                default:
                    return CollisionSystem.IsSolidType(tileType)
                        ? Color.FromArgb(255, 112, 100, 96)
                        : Color.FromArgb(255, 20, 18, 20);
            }
        }

        /// <summary>
        /// 보스 체력 상자와 칸 막대를 그린다(배경 패스). 보스가 없거나 사망했으면 그리지 않는다.
        /// </summary>
        /// <param name="r">사각형을 그릴 렌더러.</param>
        /// <param name="bossEnemy">보스 적 인스턴스. null이거나 사망 상태이면 무시된다.</param>
        private void DrawBossHudBackdrop(Renderer r, Enemy bossEnemy)
        {
            if (r == null || bossEnemy == null || !bossEnemy.Alive)
            {
                return;
            }

            float ratio = bossEnemy.MaxHealth > 0f ? bossEnemy.Health / bossEnemy.MaxHealth : 0f;
            GetBossBarRect(out float x, out float y, out float w, out float h);
            PixelUi.Frame(r, x, y, w, h, HudUnit, PixelPalette.Panel, PixelPalette.Edge);
            PixelUi.SegmentBar(r, x + 4f, y + h - 13f, w - 8f, 9f, HudUnit, ratio, PixelPalette.AccentHot, 24);
        }

        /// <summary>
        /// 스테이지 메시지, 상호작용 힌트, 보스 등장, 클리어 안내의 상자를 그린다(배경 패스).
        /// </summary>
        /// <param name="r">사각형을 그릴 렌더러. null이면 아무것도 그리지 않는다.</param>
        /// <param name="stageStatusMessage">상단에 표시할 스테이지 상태 메시지.</param>
        /// <param name="stageStatusAlpha">상태 메시지 불투명도(0~1).</param>
        /// <param name="interactPromptText">하단에 표시할 상호작용 힌트 텍스트.</param>
        /// <param name="bossIntroTimer">보스 등장 연출 남은 시간(초).</param>
        /// <param name="bossEnemy">보스 적 인스턴스.</param>
        /// <param name="victory">스테이지 클리어 여부.</param>
        private void DrawStageBackdrop(Renderer r, string stageStatusMessage, float stageStatusAlpha, string interactPromptText, float bossIntroTimer, Enemy bossEnemy, bool victory)
        {
            if (r == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(stageStatusMessage) && stageStatusAlpha > 0f)
            {
                GetStageMessageRect(r, stageStatusMessage, out float mx, out float my, out float mw, out float mh);
                DrawHudMessagePanel(r, mx, my, mw, mh, stageStatusAlpha, PixelPalette.Edge);
            }

            if (!string.IsNullOrWhiteSpace(interactPromptText))
            {
                GetInteractPromptRect(r, interactPromptText, out float px, out float py, out float pw, out float ph);
                DrawHudMessagePanel(r, px, py, pw, ph, 1f, PixelPalette.Darken(PixelPalette.Brass, 0.35f));
            }

            if (bossIntroTimer > 0f && bossEnemy != null && bossEnemy.Alive)
            {
                float alpha = Math.Min(1f, bossIntroTimer / EnemyConfig.BossIntroDuration);
                GetBossIntroRect(out float bx, out float by, out float bw, out float bh);
                DrawHudMessagePanel(r, bx, by, bw, bh, alpha, PixelPalette.Accent);
            }

            if (victory)
            {
                GetVictoryRect(out float vx, out float vy, out float vw, out float vh);
                DrawHudMessagePanel(r, vx, vy, vw, vh, 1f, PixelPalette.Brass);
            }
        }

        /// <summary>스테이지 상단 알림 상자. 글자 너비에 맞춰 늘어난다.</summary>
        private void GetStageMessageRect(Renderer r, string msg, out float x, out float y, out float w, out float h)
        {
            float textW = r.MeasureText(msg ?? string.Empty, HudSmallText).Width;
            w = Math.Min(frameW - 16f, Math.Max(160f, textW + 28f));
            h = 22f;
            x = (frameW - w) * 0.5f;
            y = 42f;
        }

        /// <summary>하단 상호작용 힌트 상자. 글자 너비에 맞춰 늘어난다.</summary>
        private void GetInteractPromptRect(Renderer r, string text, out float x, out float y, out float w, out float h)
        {
            float textW = r.MeasureText(text ?? string.Empty, HudSmallText, true).Width;
            w = Math.Min(frameW * 0.8f, textW + 24f);
            h = 20f;
            x = (frameW - w) * 0.5f;
            // 무기 그림(DrawWeaponOverlay와 같은 크기 식) 바로 위에 둔다.
            float weaponH = Math.Min((int)(frameW * 0.21f), 210);
            y = frameH - weaponH - h - 4f;
        }

        /// <summary>보스 등장 안내 상자.</summary>
        private void GetBossIntroRect(out float x, out float y, out float w, out float h)
        {
            w = Math.Min(frameW * 0.6f, 280f);
            h = 52f;
            x = (frameW - w) * 0.5f;
            y = frameH * 0.24f;
        }

        /// <summary>스테이지 클리어 안내 상자.</summary>
        private void GetVictoryRect(out float x, out float y, out float w, out float h)
        {
            w = Math.Min(frameW * 0.6f, 260f);
            h = 52f;
            x = (frameW - w) * 0.5f;
            y = frameH * 0.38f;
        }

        /// <summary>
        /// 알림 상자를 그린다. 불투명도로 상자 전체가 함께 사라진다.
        /// </summary>
        private void DrawHudMessagePanel(Renderer r, float x, float y, float w, float h, float alpha, Color edge)
        {
            if (alpha <= 0f)
            {
                return;
            }

            PixelUi.Frame(r, x, y, w, h, HudUnit, PixelPalette.Panel, edge, raised: true, opacity: Math.Min(1f, alpha));
        }

        /// <summary>
        /// 미니맵 전체(상자, 타일, 적 점, 플레이어 점, 시야 방향)를 그린다(배경 패스).
        /// 모든 타일·점은 상자 안쪽 영역으로 잘라 그려 스크롤 시에도 밖으로 새지 않는다.
        /// </summary>
        /// <param name="r">사각형을 그릴 렌더러. null이면 아무것도 그리지 않는다.</param>
        /// <param name="player">위치 및 시야 방향을 제공하는 플레이어 상태.</param>
        private void DrawMiniMap(Renderer r, Player player)
        {
            if (r == null || player == null)
            {
                return;
            }

            int[,] map = mapManager.Map;
            if (map == null)
            {
                return;
            }

            const float cellSize = MiniMapCellSize;
            float visibleTiles = MiniMapTileRadius * 2 + 1;
            float contentSize = visibleTiles * cellSize;
            const float border = 4f;

            PixelUi.Frame(r, MiniMapMargin, MiniMapMargin, contentSize + border * 2f, contentSize + border * 2f, HudUnit,
                PixelPalette.PanelDeep, PixelPalette.Edge, raised: false);
            float panelX = MiniMapMargin + border;
            float panelY = MiniMapMargin + border;
            float panelRight = panelX + contentSize;
            float panelBottom = panelY + contentSize;
            float centerPixelX = panelX + (contentSize * 0.5f);
            float centerPixelY = panelY + (contentSize * 0.5f);
            int playerTileX = (int)Math.Floor(player.Position.X);
            int playerTileY = (int)Math.Floor(player.Position.Y);
            int mapWidth = map.GetLength(0);
            int mapHeight = map.GetLength(1);

            void DrawClipped(float x, float y, float w, float h, Color color)
            {
                float left = Math.Max(x, panelX);
                float top = Math.Max(y, panelY);
                float right = Math.Min(x + w, panelRight);
                float bottom = Math.Min(y + h, panelBottom);
                if (right <= left || bottom <= top)
                {
                    return;
                }

                r.DrawRectangle(left, top, right - left, bottom - top, color);
            }

            // 플레이어 월드 좌표를 안쪽 영역 중심에 직접 매핑하고, 가장자리 반경을 한 칸 넓혀 스크롤 빈틈을 메운다.
            for (int localY = -MiniMapTileRadius - 1; localY <= MiniMapTileRadius + 1; localY++)
            {
                for (int localX = -MiniMapTileRadius - 1; localX <= MiniMapTileRadius + 1; localX++)
                {
                    int mapX = playerTileX + localX;
                    int mapY = playerTileY + localY;
                    Color cellColor = PixelPalette.Void;
                    if (mapX >= 0 && mapY >= 0 && mapX < mapWidth && mapY < mapHeight)
                    {
                        cellColor = GetMiniMapTileColor(map[mapX, mapY]);
                    }

                    float drawX = centerPixelX + ((mapX - player.Position.X) * cellSize);
                    float drawY = centerPixelY + ((mapY - player.Position.Y) * cellSize);
                    DrawClipped(drawX, drawY, cellSize, cellSize, cellColor);
                }
            }

            Enemy[] enemies = enemyManager.Enemies;
            if (enemies != null)
            {
                foreach (Enemy enemy in enemies)
                {
                    if (enemy == null || !enemy.Alive)
                    {
                        continue;
                    }

                    float enemyX = centerPixelX + ((enemy.X - player.Position.X) * cellSize);
                    float enemyY = centerPixelY + ((enemy.Y - player.Position.Y) * cellSize);
                    Color enemyColor = enemy.IsBoss
                        ? PixelPalette.AccentHot
                        : enemy.IsMiniBoss
                            ? PixelPalette.Info
                            : PixelPalette.Accent;
                    DrawClipped(enemyX - 2f, enemyY - 2f, 4f, 4f, PixelPalette.Ink);
                    DrawClipped(enemyX - 1f, enemyY - 1f, 3f, 3f, enemyColor);
                }
            }

            // 시야 방향: 플레이어 점에서 뻗는 점선
            for (int i = 2; i <= 6; i++)
            {
                float t = i * 3f;
                float dirX = centerPixelX + (player.Direction.X * t);
                float dirY = centerPixelY + (player.Direction.Y * t);
                DrawClipped(dirX - 1f, dirY - 1f, 2f, 2f, PixelUi.Fade(PixelPalette.Good, 1f - i * 0.1f));
            }

            DrawClipped(centerPixelX - 3f, centerPixelY - 3f, 6f, 6f, PixelPalette.Ink);
            DrawClipped(centerPixelX - 2f, centerPixelY - 2f, 4f, 4f, PixelPalette.Good);
        }

        /// <summary>
        /// 탄약 상자를 그린다(배경 패스).
        /// </summary>
        /// <param name="r">사각형을 그릴 렌더러. null이면 아무것도 그리지 않는다.</param>
        /// <param name="weapon">탄약 및 특수기 상태를 제공하는 무기 인스턴스.</param>
        private void DrawAmmoCounterBackdrop(Renderer r, Weapon weapon)
        {
            if (r == null)
            {
                return;
            }

            GetAmmoBoxRect(out float x, out float y, out float w, out float h);
            PixelUi.Frame(r, x, y, w, h, HudUnit, PixelPalette.Panel, PixelPalette.EdgeDim);
            r.DrawRectangle(x + 4f, y + 15f, w - 8f, 1f, PixelPalette.EdgeDim);

            // 남은 탄약 비율을 상자 아래쪽 얇은 막대로 보여 준다.
            if (weapon != null && weapon.MagazineSize > 0)
            {
                float ratio = Math.Max(0f, Math.Min(1f, weapon.CurrentAmmo / (float)weapon.MagazineSize));
                r.DrawRectangle(x + w - 34f, y + h - 8f, 28f, 3f, PixelPalette.PanelDeep);
                r.DrawRectangle(x + w - 34f, y + h - 8f, 28f * ratio, 3f, ratio <= 0.2f ? PixelPalette.Accent : PixelPalette.Brass);
            }
        }

        /// <summary>
        /// 코인 상자를 그린다(배경 패스). 탄약 상자 바로 위에 놓인다.
        /// </summary>
        /// <param name="r">사각형을 그릴 렌더러. null이면 아무것도 그리지 않는다.</param>
        private void DrawCoinHudBackdrop(Renderer r)
        {
            if (r == null)
            {
                return;
            }

            GetCoinBoxRect(out float x, out float y, out float w, out float h);
            PixelUi.Frame(r, x, y, w, h, HudUnit, PixelPalette.Panel, PixelPalette.EdgeDim);

            // 픽셀 동전 아이콘
            float cx = x + 6f;
            float cy = y + h * 0.5f - 3f;
            r.DrawRectangle(cx + 1f, cy, 4f, 6f, PixelPalette.Brass);
            r.DrawRectangle(cx, cy + 1f, 6f, 4f, PixelPalette.Brass);
            r.DrawRectangle(cx + 2f, cy + 1f, 1f, 4f, PixelPalette.Lighten(PixelPalette.Brass, 0.5f));
        }

        /// <summary>
        /// GPU 경로에서 피격 방향에 따라 화면 가장자리에 붉은 번쩍임 오버레이를
        /// 렌더러를 통해 그린다. 피격 방향에 해당하는 가장자리가 더 밝게 표시된다.
        /// </summary>
        /// <param name="r">사각형을 그릴 렌더러. null이면 아무것도 그리지 않는다.</param>
        /// <param name="player">시야 방향 계산에 사용하는 플레이어 상태.</param>
        /// <param name="damageFlash">번쩍임 강도(0~1). 0이면 아무것도 그리지 않는다.</param>
        /// <param name="damageDirX">피격 방향 벡터의 X 성분(월드 공간).</param>
        /// <param name="damageDirY">피격 방향 벡터의 Y 성분(월드 공간).</param>
        private void DrawPlayerDamageOverlay(Renderer r, Player player, float damageFlash, float damageDirX, float damageDirY)
        {
            if (r == null || damageFlash <= 0f)
            {
                return;
            }

            if (damageFlash > 1f)
            {
                damageFlash = 1f;
            }

            float dirLen = (float)Math.Sqrt((damageDirX * damageDirX) + (damageDirY * damageDirY));
            if (dirLen <= 0.001f)
            {
                damageDirX = -player.Direction.X;
                damageDirY = -player.Direction.Y;
                dirLen = 1f;
            }

            damageDirX /= dirLen;
            damageDirY /= dirLen;

            float rightX = -player.Direction.Y;
            float rightY = player.Direction.X;
            float front = Math.Max(0f, (damageDirX * player.Direction.X) + (damageDirY * player.Direction.Y));
            float back = Math.Max(0f, -((damageDirX * player.Direction.X) + (damageDirY * player.Direction.Y)));
            float right = Math.Max(0f, (damageDirX * rightX) + (damageDirY * rightY));
            float left = Math.Max(0f, -((damageDirX * rightX) + (damageDirY * rightY)));

            float maxComponent = Math.Max(Math.Max(front, back), Math.Max(left, right));
            if (maxComponent <= 0.001f)
            {
                maxComponent = 1f;
            }

            front = (float)Math.Pow(front / maxComponent, 1.6f);
            back = (float)Math.Pow(back / maxComponent, 1.6f);
            left = (float)Math.Pow(left / maxComponent, 1.6f);
            right = (float)Math.Pow(right / maxComponent, 1.6f);

            int topAlpha = (int)(82f * damageFlash * front);
            int bottomAlpha = (int)(82f * damageFlash * back);
            int leftAlpha = (int)(82f * damageFlash * left);
            int rightAlpha = (int)(82f * damageFlash * right);
            float edgeSize = Math.Max(40f, frameW * 0.065f);

            if (topAlpha > 0)
            {
                r.DrawRectangle(0f, 0f, frameW, edgeSize, Color.FromArgb(topAlpha, 140, 18, 18));
            }

            if (bottomAlpha > 0)
            {
                r.DrawRectangle(0f, frameH - edgeSize, frameW, edgeSize, Color.FromArgb(bottomAlpha, 140, 18, 18));
            }

            if (leftAlpha > 0)
            {
                r.DrawRectangle(0f, edgeSize, edgeSize, frameH - edgeSize * 2f, Color.FromArgb(leftAlpha, 140, 18, 18));
            }

            if (rightAlpha > 0)
            {
                r.DrawRectangle(frameW - edgeSize, edgeSize, edgeSize, frameH - edgeSize * 2f, Color.FromArgb(rightAlpha, 140, 18, 18));
            }
        }

        /// <summary>
        /// GPU 경로에서 무기 이미지를 화면 하단 중앙에 렌더러를 통해 그린다.
        /// 무기별 Gun/ 폴더의 sprite sheet를 우선 사용하고, 없으면 레거시 프레임으로 폴백한다.
        /// </summary>
        private void DrawWeaponOverlay(Renderer r, Weapon weapon)
        {
            if (r == null || weapon == null) return;

            int frameIndex = weapon.GetCurrentWeaponFrameIndex();
            int weaponW = Math.Min((int)(frameW * 0.21f), 210);
            int weaponH = weaponW;
            float startX = (frameW - weaponW) / 2f;
            float startY = frameH - weaponH;

            Image image = textureManager.GetWeaponFireImage(weapon.CurrentType, frameIndex);

            if (image != null)
            {
                r.DrawImage(image, startX, startY, weaponW, weaponH);
            }
        }

        /// <summary>
        /// 좌하단 상태 상자와 체력 줄을 그린다. 상태 상자에서 가장 먼저 호출되므로 상자 틀도 여기서 그린다.
        /// 체력이 30% 미만이면 막대가 깜박인다.
        /// </summary>
        /// <param name="r">사각형을 그릴 렌더러. null이면 아무것도 그리지 않는다.</param>
        /// <param name="player">현재 체력과 최대 체력을 제공하는 플레이어 상태.</param>
        private void DrawHealthBar(Renderer r, Player player)
        {
            if (r == null || player == null)
            {
                return;
            }

            GetStatusBoxRect(out float x, out float y, out float w, out float h);
            PixelUi.Frame(r, x, y, w, h, HudUnit, PixelPalette.Panel, PixelPalette.EdgeDim);

            float ratio = player.MaxHealth > 0f ? player.Health / player.MaxHealth : 0f;
            bool critical = ratio < 0.3f && ((int)(HudTime * 5f) % 2 == 0);
            DrawStatusRow(r, 1, "HP", ratio, critical ? PixelPalette.AccentHot : PixelPalette.Health, (int)Math.Ceiling(player.Health), 10);
        }

        /// <summary>
        /// 상태 상자의 보호막 줄을 그린다.
        /// </summary>
        private void DrawShieldBar(Renderer r, Player player)
        {
            if (r == null || player == null)
            {
                return;
            }

            float ratio = player.MaxShield > 0f ? player.Shield / player.MaxShield : 0f;
            DrawStatusRow(r, 0, "SH", ratio, PixelPalette.Shield, (int)Math.Ceiling(player.Shield), 10);
        }

        /// <summary>
        /// 상태 상자의 스태미나 줄을 그린다. 값 대신 짧은 막대만 보인다.
        /// </summary>
        /// <param name="r">사각형을 그릴 렌더러. null이면 아무것도 그리지 않는다.</param>
        /// <param name="player">현재 스태미나와 최대 스태미나를 제공하는 플레이어 상태.</param>
        private void DrawStaminaBar(Renderer r, Player player)
        {
            if (r == null || player == null)
            {
                return;
            }

            float ratio = player.MaxStamina > 0f ? player.Stamina / player.MaxStamina : 0f;
            DrawStatusRow(r, 2, "ST", ratio, PixelPalette.Stamina, -1, 16);
        }

        /// <summary>상태 상자 한 줄: 라벨, 칸 막대, 오른쪽 값(value가 음수면 생략).</summary>
        private void DrawStatusRow(Renderer r, int slot, string label, float ratio, Color color, int value, int segments)
        {
            GetStatusRow(slot, out float labelX, out float barX, out float barY, out float barW, out float barH, out float valueRight);
            float textY = barY + (barH - HudSmallText) * 0.5f - 1f;
            PixelUi.Text(r, label, labelX, textY, color, HudSmallText, HudUnit, bold: true);
            PixelUi.SegmentBar(r, barX, barY, barW, barH, HudUnit, ratio, color, segments);
            if (value >= 0)
            {
                PixelUi.TextRight(r, value.ToString(), valueRight, textY, PixelPalette.Text, HudSmallText, HudUnit, bold: true);
            }
        }

        /// <summary>
        /// 화면 정중앙에 픽셀 십자 조준점을 그린다. 검은 외곽선으로 밝은 배경에서도 보인다.
        /// </summary>
        /// <param name="r">사각형을 그릴 렌더러. null이면 아무것도 그리지 않는다.</param>
        private void DrawCrosshair(Renderer r)
        {
            if (r == null)
            {
                return;
            }

            float cx = frameW / 2;
            float cy = frameH / 2;
            const float gap = 3f;
            const float len = 4f;
            Color ink = PixelUi.Fade(PixelPalette.Ink, 0.8f);

            // 외곽선
            r.DrawRectangle(cx - 1f, cy - gap - len - 1f, 3f, len + 2f, ink);
            r.DrawRectangle(cx - 1f, cy + gap, 3f, len + 2f, ink);
            r.DrawRectangle(cx - gap - len - 1f, cy - 1f, len + 2f, 3f, ink);
            r.DrawRectangle(cx + gap, cy - 1f, len + 2f, 3f, ink);
            r.DrawRectangle(cx - 1f, cy - 1f, 3f, 3f, ink);

            // 본체
            Color line = PixelPalette.Text;
            r.DrawRectangle(cx, cy - gap - len, 1f, len, line);
            r.DrawRectangle(cx, cy + gap + 1f, 1f, len, line);
            r.DrawRectangle(cx - gap - len, cy, len, 1f, line);
            r.DrawRectangle(cx + gap + 1f, cy, len, 1f, line);
            r.DrawRectangle(cx, cy, 1f, 1f, PixelPalette.AccentHot);
        }

        /// <summary>
        /// 명중(흰색)/처치(빨강) 피드백을 조준점 주변 대각선 픽셀 꺾쇠로 짧게 표시한다.
        /// </summary>
        private void DrawHitMarker(Renderer r, float hitMarkerAlpha, float killMarkerAlpha)
        {
            if (r == null)
            {
                return;
            }

            hitMarkerAlpha = Math.Max(0f, Math.Min(1f, hitMarkerAlpha));
            killMarkerAlpha = Math.Max(0f, Math.Min(1f, killMarkerAlpha));
            if (hitMarkerAlpha <= 0f && killMarkerAlpha <= 0f)
            {
                return;
            }

            int cx = frameW / 2;
            int cy = frameH / 2;
            if (hitMarkerAlpha > 0f)
            {
                DrawDiagonalMarker(r, cx, cy, 6f + (1f - hitMarkerAlpha) * 3f, 3, 1f, PixelUi.Fade(PixelPalette.Text, hitMarkerAlpha));
            }

            if (killMarkerAlpha > 0f)
            {
                DrawDiagonalMarker(r, cx, cy, 9f + (1f - killMarkerAlpha) * 5f, 4, 2f, PixelUi.Fade(PixelPalette.AccentHot, killMarkerAlpha));
                r.DrawRectangle(cx - 2, cy - 2, 5, 5, PixelUi.Fade(PixelPalette.Accent, killMarkerAlpha * 0.7f));
            }
        }

        /// <summary>네 대각선 방향으로 계단식 픽셀 줄을 그린다.</summary>
        private static void DrawDiagonalMarker(Renderer r, int cx, int cy, float start, int steps, float block, Color color)
        {
            for (int i = 0; i < steps; i++)
            {
                float o = start + i * block;
                r.DrawRectangle(cx - o - block, cy - o - block, block, block, color);
                r.DrawRectangle(cx + o + 1f, cy - o - block, block, block, color);
                r.DrawRectangle(cx - o - block, cy + o + 1f, block, block, color);
                r.DrawRectangle(cx + o + 1f, cy + o + 1f, block, block, color);
            }
        }

        /// <summary>발사 불가 상태를 조준점 아래에 짧게 표시한다.</summary>
        private void DrawWeaponStatusFeedback(Renderer r, string statusText, float alpha)
        {
            if (r == null || string.IsNullOrWhiteSpace(statusText))
            {
                return;
            }

            alpha = Math.Max(0f, Math.Min(1f, alpha));
            if (alpha <= 0f)
            {
                return;
            }

            Color color = statusText.IndexOf("AMMO", StringComparison.OrdinalIgnoreCase) >= 0
                ? PixelPalette.AccentHot
                : PixelPalette.Brass;
            PixelUi.TextCentered(r, statusText, frameW * 0.5f, (frameH * 0.5f) + 22f, PixelUi.Fade(color, alpha), HudSmallText, HudUnit, bold: true);
        }

        /// <summary>보상 드롭/획득 알림을 우하단 상자들 위에 짧게 띄운다.</summary>
        private void DrawPickupToast(Renderer r, string toastText, float alpha)
        {
            if (r == null || string.IsNullOrWhiteSpace(toastText))
            {
                return;
            }

            alpha = Math.Max(0f, Math.Min(1f, alpha));
            if (alpha <= 0f)
            {
                return;
            }

            Color accent = GetPickupToastAccentColor(toastText);
            float textW = r.MeasureText(toastText, HudSmallText, true).Width;
            float panelW = Math.Min(180f, textW + 22f);
            float panelH = 18f;
            GetCoinBoxRect(out float coinX, out float coinY, out float coinW, out _);
            float panelX = coinX + coinW - panelW;
            float panelY = coinY - RightBoxGap - panelH - ((1f - alpha) * 6f);
            PixelUi.Frame(r, panelX, panelY, panelW, panelH, HudUnit, PixelPalette.Panel, PixelPalette.Darken(accent, 0.4f), raised: true, opacity: alpha);
            r.DrawRectangle(panelX + 4f, panelY + 4f, 2f, panelH - 8f, PixelUi.Fade(accent, alpha));
            PixelUi.Text(r, toastText, panelX + 10f, panelY + (panelH - HudSmallText) * 0.5f - 1f, PixelUi.Fade(accent, alpha), HudSmallText, HudUnit, bold: true);
        }

        private static Color GetPickupToastAccentColor(string text)
        {
            if (text.IndexOf("COIN", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return PixelPalette.Brass;
            }

            if (text.IndexOf("AMMO", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return PixelPalette.Info;
            }

            return PixelPalette.AccentHot;
        }

        /// <summary>
        /// 사망 연출 전체(붉은 색조, 위아래 어두운 띠, "YOU DIED" 상자와 문구)를 그린다.
        /// </summary>
        /// <param name="r">사각형과 텍스트를 그릴 렌더러. null이면 아무것도 그리지 않는다.</param>
        /// <param name="deathProgress">사망 연출 진행 비율(0=시작, 1=완료).</param>
        private void DrawDeathOverlay(Renderer r, float deathProgress)
        {
            if (r == null)
            {
                return;
            }

            float eased = EaseOutCubic(deathProgress);
            r.DrawRectangle(0f, 0f, frameW, frameH, Color.FromArgb((int)(60f + 140f * eased), 110, 6, 8));
            int bandAlpha = (int)(40f + 140f * eased);
            r.DrawRectangle(0f, 0f, frameW, frameH * 0.18f * eased, Color.FromArgb(bandAlpha, 0, 0, 0));
            r.DrawRectangle(0f, frameH * (1f - 0.18f * eased), frameW, frameH * 0.18f * eased, Color.FromArgb(bandAlpha, 0, 0, 0));

            float uiFade = deathProgress <= 0.2f ? 0f : EaseOutCubic((deathProgress - 0.2f) / 0.8f);
            if (uiFade > 0f)
            {
                GetDeathPanelRect(out float x, out float y, out float w, out float h);
                PixelUi.Frame(r, x, y, w, h, HudUnit, PixelPalette.PanelDeep, PixelPalette.Accent, raised: true, opacity: uiFade);
            }

            DrawDeathPresentation(r, deathProgress);
        }
    }
}
