// DoomSpriteGen — generates Doom-style 64×64 pixel-art sprite sheets
// for My2DEngine enemies, bosses and weapons.
// Internal canvas: 32×32 (classic Doom pixel art scale), 2× upscaled to 64×64.
// Sheet layout: 5 rows (Idle=4f, Move=6f, Attack=4f, Special=5f, Death=6f)

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

return Gen.Run();

static class Gen
{
// ── Doom palette ─────────────────────────────────────────────────────────────
static Color C(int r, int g, int b) => Color.FromArgb(255, r, g, b);

static readonly Color K   = C(  8,  6,  6);   // black outline
static readonly Color DB  = C( 52, 28, 12);   // dark brown
static readonly Color B   = C( 92, 52, 22);   // brown
static readonly Color T   = C(148,100, 46);   // tan
static readonly Color LT  = C(196,156, 82);   // light tan
static readonly Color DR  = C(112, 12,  8);   // dark red
static readonly Color R   = C(172, 24, 12);   // red
static readonly Color BR  = C(216, 48, 24);   // bright red / blood
static readonly Color DO  = C( 36, 48, 18);   // dark olive
static readonly Color OL  = C( 68, 76, 34);   // olive
static readonly Color KH  = C(118,120, 56);   // khaki
static readonly Color LK  = C(164,168, 82);   // light khaki
static readonly Color DG  = C( 44, 44, 46);   // dark gray
static readonly Color GR  = C( 90, 90, 96);   // gray
static readonly Color LG  = C(152,152,162);   // light gray
static readonly Color FL  = C(182, 98, 58);   // flesh
static readonly Color LF  = C(218,152, 92);   // light flesh
static readonly Color DF  = C(138, 62, 34);   // dark flesh
static readonly Color BN  = C(194,166,110);   // bone
static readonly Color LN  = C(230,204,148);   // light bone
static readonly Color FO  = C(222, 82, 16);   // fire orange
static readonly Color FY  = C(242,170, 30);   // fire yellow
static readonly Color DM  = C( 42, 42, 50);   // dark metal
static readonly Color MT  = C( 78, 78, 90);   // metal
static readonly Color LM  = C(130,130,148);   // light metal
static readonly Color PK  = C(194,102, 74);   // pink-brown
static readonly Color LP  = C(222,142,102);   // light pink
static readonly Color DP  = C(148, 72, 50);   // dark pink
static readonly Color WH  = C(228,222,200);   // near-white
static readonly Color WD  = C(102, 58, 26);   // wood
static readonly Color LW  = C(144, 90, 46);   // light wood
static readonly Color CW  = C(192,188,178);   // coat/paper white
static readonly Color CD  = C(148,144,136);   // coat shadow
static readonly Color __  = Color.Transparent;

const int SZ     = 32;   // internal canvas size per frame
const int OUT_SZ = 64;   // final output size (TextureSize)
static readonly int[] RowFrameCounts = { 4, 6, 4, 5, 6 }; // Idle,Move,Attack,Special,Death
const int ROWS = 5;
const int COLS = 6;

enum Anim { Idle, Move, Attack, Special, Death }

// ── Canvas ───────────────────────────────────────────────────────────────────
sealed class Canvas
{
    public readonly int W, H;
    readonly Color[] px;
    public Canvas(int w, int h) { W = w; H = h; px = new Color[w * h]; }
    public void Clear() { Array.Fill(px, Color.Transparent); }
    public Color Get(int x, int y) => (x >= 0 && x < W && y >= 0 && y < H) ? px[y * W + x] : Color.Transparent;
    public void Set(int x, int y, Color c) { if (x >= 0 && x < W && y >= 0 && y < H) px[y * W + x] = c; }
    public void Blend(int x, int y, Color c, float alpha = 1f)
    {
        if (x < 0 || x >= W || y < 0 || y >= H) return;
        if (alpha >= 0.99f) { px[y * W + x] = c; return; }
        Color orig = px[y * W + x];
        px[y * W + x] = Color.FromArgb(
            255,
            (int)(orig.R + (c.R - orig.R) * alpha),
            (int)(orig.G + (c.G - orig.G) * alpha),
            (int)(orig.B + (c.B - orig.B) * alpha));
    }

    public void Rect(int x, int y, int w, int h, Color c)
    { for (int dy = 0; dy < h; dy++) for (int dx = 0; dx < w; dx++) Set(x+dx, y+dy, c); }

    public void Ellipse(int cx, int cy, int rx, int ry, Color c)
    {
        for (int dy = -ry; dy <= ry; dy++)
            for (int dx = -rx; dx <= rx; dx++)
                if ((float)(dx*dx)/(rx*rx+0.5f)+(float)(dy*dy)/(ry*ry+0.5f) <= 1f)
                    Set(cx+dx, cy+dy, c);
    }

    public void Tri(int x1,int y1,int x2,int y2,int x3,int y3, Color c)
    {
        int minY = Math.Max(0, Math.Min(y1, Math.Min(y2, y3)));
        int maxY = Math.Min(H-1, Math.Max(y1, Math.Max(y2, y3)));
        for (int y = minY; y <= maxY; y++)
        {
            float xA = EdgeX(x1,y1,x2,y2,y);
            float xB = EdgeX(x2,y2,x3,y3,y);
            float xC = EdgeX(x3,y3,x1,y1,y);
            float left  = Math.Min(xA, Math.Min(xB, xC));
            float right = Math.Max(xA, Math.Max(xB, xC));
            for (int x = (int)Math.Max(0, left); x <= (int)Math.Min(W-1, right); x++)
                Set(x, y, c);
        }
    }
    static float EdgeX(int x1,int y1,int x2,int y2,int y)
    {
        if (y2 == y1) return (x1+x2)*0.5f;
        return x1 + (float)(y - y1) / (y2 - y1) * (x2 - x1);
    }

    // 1-pixel black outline around all opaque pixels
    public void Outline(int fx, int fy, int fw, int fh, Color oc)
    {
        bool[,] mark = new bool[fw, fh];
        for (int y = 0; y < fh; y++)
            for (int x = 0; x < fw; x++)
            {
                if (Get(fx+x, fy+y).A > 80) continue;
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x+dx, ny = y+dy;
                    if (nx >= 0 && nx < fw && ny >= 0 && ny < fh && Get(fx+nx, fy+ny).A > 80)
                    { mark[x, y] = true; goto next; }
                }
                next:;
            }
        for (int y = 0; y < fh; y++)
            for (int x = 0; x < fw; x++)
                if (mark[x, y]) px[(fy+y)*W + (fx+x)] = oc;
    }

    // 2× nearest-neighbor upscale of a SZ×SZ sub-region into a 2*SZ×2*SZ target canvas
    public void Upscale2x(Canvas dst, int srcX, int srcY, int dstX, int dstY)
    {
        for (int y = 0; y < SZ; y++)
            for (int x = 0; x < SZ; x++)
            {
                Color c = Get(srcX+x, srcY+y);
                dst.Set(dstX + x*2,   dstY + y*2,   c);
                dst.Set(dstX + x*2+1, dstY + y*2,   c);
                dst.Set(dstX + x*2,   dstY + y*2+1, c);
                dst.Set(dstX + x*2+1, dstY + y*2+1, c);
            }
    }

    public Bitmap ToBitmap()
    {
        var bmp = new Bitmap(W, H, PixelFormat.Format32bppArgb);
        var bd = bmp.LockBits(new Rectangle(0,0,W,H), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        byte[] buf = new byte[Math.Abs(bd.Stride)*H];
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
        {
            Color c = px[y*W+x];
            int i = y*bd.Stride + x*4;
            buf[i] = c.B; buf[i+1] = c.G; buf[i+2] = c.R; buf[i+3] = c.A;
        }
        Marshal.Copy(buf, 0, bd.Scan0, buf.Length);
        bmp.UnlockBits(bd);
        return bmp;
    }
}

// ── Animation helpers ─────────────────────────────────────────────────────────
static int Bob(Anim a, int f, int amp = 2)
    => a == Anim.Idle ? (int)(Math.Sin(f * Math.PI * 0.5) * amp * 0.5) :
       a == Anim.Move ? (int)(Math.Sin(f * Math.PI / 3.0) * amp) : 0;

static int LegFwd(Anim a, int f, bool left)
    => a != Anim.Move ? 0 : (left ? (f % 2 == 0 ? -2 : 2) : (f % 2 == 0 ? 2 : -2));

static int ArmSwing(Anim a, int f, bool left)
    => a != Anim.Move ? 0 : (left ? (f % 2 == 0 ? -3 : 3) : (f % 2 == 0 ? 3 : -3));

static float DeathProg(Anim a, int f, int total) =>
    a == Anim.Death ? (float)f / Math.Max(1, total - 1) : 0f;

// ── Sprite draw functions ─────────────────────────────────────────────────────

// Zombie Scientist — olive-uniformed undead soldier, shredded lab coat, pistol
static void DrawZombie(Canvas c, int fx, int fy, Anim a, int f)
{
    c.Clear();
    int cx = fx+16, bot = fy+30;
    int bob = Bob(a, f, 2);
    int oy = -bob; // vertical offset applied additively to Y coords

    // olive military cap
    c.Rect(cx-6, bot-28+oy, 13, 2, OL);
    c.Rect(cx-5, bot-30+oy, 11, 3, KH);
    // head
    c.Ellipse(cx, bot-23+oy, 5, 6, FL);
    c.Ellipse(cx, bot-23+oy, 3, 4, LF);
    // eyes
    c.Set(cx-2, bot-25+oy, DB); c.Set(cx+2, bot-25+oy, DB);
    // neck
    c.Rect(cx-2, bot-16+oy, 5, 3, FL);
    // lab coat — torn, blood-stained
    c.Ellipse(cx, bot-10+oy, 9, 8, CW);
    c.Rect(cx-5, bot-16+oy, 10, 9, CW);
    // blood splatters on coat
    c.Ellipse(cx-3, bot-12+oy, 3, 2, DR);
    c.Set(cx-1, bot-10+oy, R);
    c.Set(cx+2, bot-9+oy, R);
    // torn coat bottom reveal: olive uniform beneath
    c.Rect(cx-4, bot-7+oy, 3, 4, OL);
    c.Rect(cx+1, bot-8+oy, 4, 5, OL);
    // arms
    int la = ArmSwing(a, f, true);
    int ra = ArmSwing(a, f, false);
    c.Rect(cx-11, bot-16+oy+la, 4, 11, CW);  // left arm sleeve
    c.Ellipse(cx-9, bot-5+oy+la, 2, 2, DF);  // left hand
    // right arm + pistol
    if (a == Anim.Attack && f <= 1)
    {
        c.Rect(cx+7, bot-20+oy, 4, 9, CW);       // arm raised
        c.Rect(cx+8, bot-22+oy, 5, 3, DM);       // pistol
        c.Rect(cx+11, bot-24+oy, 2, 3, DM);      // barrel
    }
    else
    {
        c.Rect(cx+7, bot-16+oy+ra, 4, 9, CW);
        c.Rect(cx+8, bot-8+oy+ra, 5, 3, DM);
        c.Rect(cx+11, bot-10+oy+ra, 2, 4, DM);
    }
    // legs
    int ll = LegFwd(a, f, true);
    int rl = LegFwd(a, f, false);
    c.Rect(cx-8, bot-5+oy, 5, 7+ll, OL);
    c.Rect(cx+3, bot-5+oy, 5, 7-rl, OL);
    c.Rect(cx-9, bot+2+oy, 6, 2, DB);   // left boot
    c.Rect(cx+2, bot+2+oy, 6, 2, DB);   // right boot

    // death: fall backward
    float dp = DeathProg(a, f, 6);
    if (dp > 0f)
    {
        c.Clear();
        DrawZombieFallen(c, fx, fy, dp);
    }
    c.Outline(fx, fy, SZ, SZ, K);
}

static void DrawZombieFallen(Canvas c, int fx, int fy, float t)
{
    int cx = fx+16, by = fy+28;
    // Body rotated/laid out
    int yOff = (int)(t * 8);
    c.Ellipse(cx, by-5+yOff, 12, 4, CW);
    c.Ellipse(cx, by-5+yOff, 8, 3, OL);
    c.Ellipse(cx+12, by-4+yOff, 5, 4, FL);
    c.Rect(cx-12, by-3+yOff, 25, 4, OL);
    if (t > 0.5f) c.Rect(cx-8, by-4+yOff, 3, 3, DR); // blood pool
}

// Blind Pinky — massive bull-headed demon, charges blindly
static void DrawPinky(Canvas c, int fx, int fy, Anim a, int f)
{
    c.Clear();
    int cx = fx+16, bot = fy+28;
    int bob = Bob(a, f, 3);
    int oy = -bob;

    // massive head/body (pink-brown)
    c.Ellipse(cx, bot-14+oy, 14, 12, PK);
    c.Ellipse(cx, bot-14+oy, 12, 10, LP);

    // horns
    c.Tri(cx-14, bot-14+oy, cx-8, bot-22+oy, cx-10, bot-13+oy, DF);
    c.Tri(cx+14, bot-14+oy, cx+10, bot-13+oy, cx+8, bot-22+oy, DF);

    // eyes (blind — scarred over white)
    c.Ellipse(cx-6, bot-18+oy, 3, 3, WH);
    c.Ellipse(cx+6, bot-18+oy, 3, 3, WH);
    c.Set(cx-6, bot-18+oy, LG); c.Set(cx+6, bot-18+oy, LG);

    // mouth — open on attack
    int mouthOpen = (a == Anim.Attack) ? 7 : (a == Anim.Move ? 5 : 4);
    c.Rect(cx-9, bot-12+oy, 18, mouthOpen, DG);
    // teeth
    for (int i = 0; i < 4; i++)
    {
        c.Tri(cx-8+i*5, bot-12+oy, cx-6+i*5, bot-12+oy, cx-7+i*5, bot-12-3+oy, WH);
        c.Tri(cx-8+i*5, bot-12+mouthOpen+oy, cx-6+i*5, bot-12+mouthOpen+oy, cx-7+i*5, bot-12+mouthOpen+3+oy, WH);
    }
    // tongue on attack
    if (a == Anim.Attack && f <= 2) c.Ellipse(cx, bot-9+oy, 4, 3, R);

    // tiny stub legs
    int ll = LegFwd(a, f, true)*2;
    int rl = LegFwd(a, f, false)*2;
    c.Rect(cx-11, bot-2+oy, 6, 5+ll, DP);
    c.Rect(cx+5,  bot-2+oy, 6, 5-rl, DP);
    c.Rect(cx-12, bot+3+oy, 7, 2, DB);
    c.Rect(cx+5,  bot+3+oy, 7, 2, DB);

    float dp = DeathProg(a, f, 6);
    if (dp > 0f) { c.Ellipse(cx, bot-8, 15+(int)(dp*4), 10-(int)(dp*4), PK); }

    c.Outline(fx, fy, SZ, SZ, K);
}

// Slime Imp — hunched brown demon with horns and claws
static void DrawImp(Canvas c, int fx, int fy, Anim a, int f)
{
    c.Clear();
    int cx = fx+16, bot = fy+30;
    int bob = Bob(a, f, 2);
    int oy = -bob;

    // head
    c.Ellipse(cx, bot-24+oy, 6, 6, B);
    c.Ellipse(cx, bot-24+oy, 4, 4, T);
    // horns
    c.Tri(cx-5, bot-28+oy, cx-3, bot-28+oy, cx-3, bot-22+oy, DB);
    c.Tri(cx+5, bot-28+oy, cx+3, bot-28+oy, cx+3, bot-22+oy, DB);
    // orange glowing eyes
    c.Set(cx-2, bot-25+oy, FO); c.Set(cx+2, bot-25+oy, FO);
    c.Set(cx-3, bot-25+oy, FY); c.Set(cx+3, bot-25+oy, FY);
    // neck + hunched torso
    c.Rect(cx-2, bot-17+oy, 5, 3, B);
    c.Ellipse(cx, bot-11+oy, 8, 8, B);
    c.Ellipse(cx, bot-11+oy, 6, 6, T);
    // belly spikes
    c.Tri(cx-6, bot-5+oy, cx-4, bot-5+oy, cx-5, bot-1+oy, DB);
    c.Tri(cx+4, bot-5+oy, cx+6, bot-5+oy, cx+5, bot-1+oy, DB);
    // clawed arms — hunched forward
    int la = ArmSwing(a, f, true);
    int ra = ArmSwing(a, f, false);
    if (a == Anim.Attack && f <= 1)
    {
        // claws thrust forward
        c.Rect(cx-14, bot-16+oy, 5, 4, B);
        c.Tri(cx-14, bot-14+oy, cx-17, bot-17+oy, cx-17, bot-11+oy, DF);
        c.Tri(cx-14, bot-14+oy, cx-17, bot-12+oy, cx-14, bot-11+oy, DB); // claw tip
        c.Rect(cx+9, bot-16+oy, 5, 4, B);
        c.Tri(cx+14, bot-14+oy, cx+17, bot-17+oy, cx+17, bot-11+oy, DF);
    }
    else
    {
        c.Rect(cx-12, bot-14+oy+la, 5, 8, B);
        c.Tri(cx-11, bot-6+oy+la, cx-14, bot-4+oy+la, cx-9, bot-4+oy+la, DF); // claw
        c.Rect(cx+7, bot-14+oy+ra, 5, 8, B);
        c.Tri(cx+11, bot-6+oy+ra, cx+8, bot-4+oy+ra, cx+14, bot-4+oy+ra, DF);
    }
    // legs
    int ll = LegFwd(a, f, true);
    int rl = LegFwd(a, f, false);
    c.Rect(cx-7, bot-4+oy, 5, 6+ll, DB);
    c.Rect(cx+2, bot-4+oy, 5, 6-rl, DB);
    c.Rect(cx-8, bot+2+oy, 6, 2, B);
    c.Rect(cx+2, bot+2+oy, 6, 2, B);

    float dp = DeathProg(a, f, 6);
    if (dp > 0.4f) { c.Rect(fx+2, fy+26, 28, 6, DR); }

    c.Outline(fx, fy, SZ, SZ, K);
}

// Blood Ghost — flying flaming skull (Lost Soul)
static void DrawGhost(Canvas c, int fx, int fy, Anim a, int f)
{
    c.Clear();
    int cx = fx+16, cy = fy+14;
    int bob = Bob(a, f, 3);
    int oy = -bob;

    float dp = DeathProg(a, f, 6);

    // fire trail below skull (more on move/attack)
    int fireH = (a == Anim.Move || a == Anim.Attack) ? 14 : 10;
    for (int i = 0; i < 5; i++)
    {
        int fw = fireH - i*2;
        if (fw <= 0) break;
        c.Ellipse(cx + (i%2==0?1:-1), cy+10+i*2+oy, fw/2+1, 2, i==0?FO:FY);
    }
    if (dp < 0.6f) // fire vanishes on death
    {
        c.Ellipse(cx, cy+8+oy, 5, 3, FO);
        c.Ellipse(cx, cy+10+oy, 3, 2, FY);
    }

    // skull shape
    int alpha = dp > 0f ? (int)(255*(1f-dp)) : 255;
    Color boneC = Color.FromArgb(alpha, BN);
    Color boneLc = Color.FromArgb(alpha, LN);
    c.Ellipse(cx, cy+oy, 11, 10, boneC);
    c.Ellipse(cx, cy-2+oy, 9, 9, boneLc);
    // cheekbones
    c.Ellipse(cx-7, cy+4+oy, 3, 4, boneC);
    c.Ellipse(cx+7, cy+4+oy, 3, 4, boneC);
    // jaw
    c.Rect(cx-6, cy+7+oy, 13, 4, boneC);
    // eye sockets — glowing orange
    c.Ellipse(cx-4, cy+oy, 3, 3, DG);
    c.Ellipse(cx+4, cy+oy, 3, 3, DG);
    c.Set(cx-4, cy+oy, FO); c.Set(cx+4, cy+oy, FO);
    c.Set(cx-4, cy-1+oy, FY); c.Set(cx+4, cy-1+oy, FY);
    // nose cavity
    c.Tri(cx-1, cy+3+oy, cx+1, cy+3+oy, cx, cy+5+oy, DG);
    // teeth
    for (int i = 0; i < 3; i++)
        c.Rect(cx-4+i*4, cy+8+oy, 2, 3, WH);
    // crack on death
    if (dp > 0.3f)
    {
        c.Rect(cx, cy-4+oy, 1, 10, DG);
        c.Rect(cx-2, cy+2+oy, 4, 1, DG);
    }

    c.Outline(fx, fy, SZ, SZ, K);
}

// Beam Revenant — armored skeleton with rocket launchers
static void DrawRevenant(Canvas c, int fx, int fy, Anim a, int f)
{
    c.Clear();
    int cx = fx+16, bot = fy+30;
    int bob = Bob(a, f, 2);
    int oy = -bob;

    // skull head
    c.Ellipse(cx, bot-26+oy, 5, 6, BN);
    c.Ellipse(cx, bot-27+oy, 4, 5, LN);
    c.Ellipse(cx-2, bot-28+oy, 2, 2, DG);
    c.Ellipse(cx+2, bot-28+oy, 2, 2, DG);
    c.Set(cx-2, bot-28+oy, FO); c.Set(cx+2, bot-28+oy, FO);
    // jaw
    c.Rect(cx-3, bot-21+oy, 7, 3, BN);
    for (int i = 0; i < 3; i++) c.Set(cx-2+i*2, bot-19+oy, WH);

    // spine
    for (int i = 0; i < 10; i++) c.Set(cx, bot-18+i+oy, (i%2==0)?BN:GR);

    // ribcage (visible)
    for (int rib = 0; rib < 4; rib++)
    {
        int ry = bot-18+rib*3+oy;
        c.Rect(cx-6, ry, 4, 2, BN);
        c.Rect(cx+2,  ry, 4, 2, BN);
    }

    // metal shoulder pads with rocket launchers
    c.Rect(cx-13, bot-20+oy, 6, 5, MT);
    c.Rect(cx+7,  bot-20+oy, 6, 5, MT);
    c.Rect(cx-14, bot-22+oy, 4, 3, DM);  // pad highlight left
    c.Rect(cx+10, bot-22+oy, 4, 3, DM);
    // rocket tubes
    if (a == Anim.Attack && f <= 2)
    {
        c.Rect(cx-16, bot-24+oy, 7, 3, MT); // rocket tube left extended
        c.Rect(cx-16, bot-24+oy, 2, 3, DM); c.Set(cx-16, bot-23+oy, FO); // muzzle flash
        c.Rect(cx+9,  bot-24+oy, 7, 3, MT);
        c.Set(cx+15, bot-23+oy, FO);
    }
    else
    {
        c.Rect(cx-15, bot-23+oy, 5, 3, MT);
        c.Rect(cx+10, bot-23+oy, 5, 3, MT);
    }

    // skeletal arms
    int la = ArmSwing(a, f, true);
    int ra = ArmSwing(a, f, false);
    c.Rect(cx-10, bot-16+oy+la, 3, 10, BN);
    c.Rect(cx+7,  bot-16+oy+ra, 3, 10, BN);

    // pelvis + legs
    c.Rect(cx-5, bot-7+oy, 10, 4, BN);
    int ll = LegFwd(a, f, true);
    int rl = LegFwd(a, f, false);
    c.Rect(cx-7, bot-3+oy, 3, 5+ll, BN);
    c.Rect(cx+4, bot-3+oy, 3, 5-rl, BN);
    c.Rect(cx-8, bot+2+oy, 5, 2, GR);
    c.Rect(cx+3, bot+2+oy, 5, 2, GR);

    float dp = DeathProg(a, f, 6);
    if (dp > 0.3f)
    {
        // scatter bones
        c.Rect(fx+2, fy+24, 10, 3, BN); c.Rect(fx+18, fy+26, 8, 2, BN);
    }

    c.Outline(fx, fy, SZ, SZ, K);
}

// Hellion — small fast imp, scurrying creature
static void DrawHellion(Canvas c, int fx, int fy, Anim a, int f)
{
    c.Clear();
    int cx = fx+16, bot = fy+30;
    int bob = Bob(a, f, 3);
    int oy = -bob;

    // small round head
    c.Ellipse(cx, bot-20+oy, 5, 5, FO);
    c.Ellipse(cx, bot-20+oy, 3, 3, T);
    // tiny horns
    c.Tri(cx-4, bot-23+oy, cx-2, bot-23+oy, cx-3, bot-26+oy, DR);
    c.Tri(cx+4, bot-23+oy, cx+2, bot-23+oy, cx+3, bot-26+oy, DR);
    // yellow eyes
    c.Set(cx-2, bot-21+oy, FY); c.Set(cx+2, bot-21+oy, FY);
    c.Set(cx-2, bot-20+oy, FO); c.Set(cx+2, bot-20+oy, FO);
    // neck
    c.Rect(cx-2, bot-14+oy, 5, 3, FO);
    // compact hunched body
    c.Ellipse(cx, bot-9+oy, 7, 7, B);
    c.Ellipse(cx, bot-9+oy, 5, 5, FO);
    // stubby arms with claws
    int la = ArmSwing(a, f, true)*2;
    int ra = ArmSwing(a, f, false)*2;
    c.Rect(cx-10, bot-12+oy+la, 3, 7, B);
    c.Tri(cx-10, bot-5+oy+la, cx-13, bot-3+oy+la, cx-8, bot-3+oy+la, DF);
    c.Rect(cx+7,  bot-12+oy+ra, 3, 7, B);
    c.Tri(cx+10,  bot-5+oy+ra, cx+7, bot-3+oy+ra, cx+13, bot-3+oy+ra, DF);
    // legs — always slightly crouched
    int ll = LegFwd(a, f, true)*2;
    int rl = LegFwd(a, f, false)*2;
    c.Rect(cx-6, bot-3+oy, 4, 5+ll, DR);
    c.Rect(cx+2, bot-3+oy, 4, 5-rl, DR);
    c.Rect(cx-7, bot+2+oy, 5, 2, DB);
    c.Rect(cx+2, bot+2+oy, 5, 2, DB);

    // death: burst of fire
    float dp = DeathProg(a, f, 6);
    if (dp > 0f)
    {
        c.Ellipse(cx, bot-8+oy, 5+(int)(dp*8), 5+(int)(dp*6), FO);
        c.Ellipse(cx, bot-8+oy, 3+(int)(dp*4), 3+(int)(dp*3), FY);
    }

    c.Outline(fx, fy, SZ, SZ, K);
}

// ── BOSS SPRITES ─────────────────────────────────────────────────────────────

// Azazel — Baron of Hell (massive muscular demon, hellfire fists)
static void DrawAzazel(Canvas c, int fx, int fy, Anim a, int f)
{
    c.Clear();
    int cx = fx+16, bot = fy+30;
    int bob = Bob(a, f, 1);
    int oy = -bob;

    // curved goat horns
    c.Tri(cx-8, bot-30+oy, cx-14, bot-30+oy, cx-10, bot-24+oy, B);
    c.Tri(cx+8, bot-30+oy, cx+14, bot-30+oy, cx+10, bot-24+oy, B);
    c.Ellipse(cx-12, bot-27+oy, 2, 3, DB);
    c.Ellipse(cx+12, bot-27+oy, 2, 3, DB);

    // large head
    c.Ellipse(cx, bot-24+oy, 8, 8, OL);
    c.Ellipse(cx, bot-24+oy, 6, 6, KH);
    // brow ridge
    c.Rect(cx-7, bot-28+oy, 14, 3, OL);
    // red eyes, glowing
    c.Ellipse(cx-3, bot-26+oy, 2, 2, DR);
    c.Ellipse(cx+3, bot-26+oy, 2, 2, DR);
    c.Set(cx-3, bot-26+oy, BR); c.Set(cx+3, bot-26+oy, BR);
    // muzzle / snout
    c.Ellipse(cx, bot-20+oy, 5, 4, OL);
    c.Rect(cx-3, bot-20+oy, 7, 2, DO);
    // teeth
    c.Set(cx-1, bot-18+oy, WH); c.Set(cx+1, bot-18+oy, WH);

    // massive chest
    c.Ellipse(cx, bot-13+oy, 13, 9, DO);
    c.Ellipse(cx, bot-14+oy, 11, 8, OL);
    // pectoral shading
    c.Ellipse(cx-5, bot-15+oy, 5, 4, KH);
    c.Ellipse(cx+5, bot-15+oy, 5, 4, KH);

    // muscular arms
    int la = ArmSwing(a, f, true);
    int ra = ArmSwing(a, f, false);
    c.Ellipse(cx-13, bot-14+oy+la, 4, 6, DO);
    c.Ellipse(cx-13, bot-8+oy+la, 4, 5, OL);
    c.Ellipse(cx+13, bot-14+oy+ra, 4, 6, DO);
    c.Ellipse(cx+13, bot-8+oy+ra, 4, 5, OL);

    // hellfire fists
    Color fistFire = (a == Anim.Attack && f <= 1) ? FY : FO;
    c.Ellipse(cx-13, bot-3+oy+la, 5, 4, fistFire);
    c.Ellipse(cx+13, bot-3+oy+ra, 5, 4, fistFire);
    c.Set(cx-13, bot-2+oy+la, FY); c.Set(cx+13, bot-2+oy+ra, FY);

    // lower body + legs (digitigrade)
    c.Ellipse(cx, bot-4+oy, 9, 6, DO);
    int ll = LegFwd(a, f, true)*2;
    int rl = LegFwd(a, f, false)*2;
    // upper legs
    c.Rect(cx-10, bot-4+oy, 5, 7+ll, OL);
    c.Rect(cx+5,  bot-4+oy, 5, 7-rl, OL);
    // hooves
    c.Rect(cx-11, bot+3+oy, 7, 2, DB);
    c.Rect(cx+4,  bot+3+oy, 7, 2, DB);
    c.Rect(cx-10, bot+5+oy, 5, 2, B);
    c.Rect(cx+5,  bot+5+oy, 5, 2, B);

    // death: explode
    float dp = DeathProg(a, f, 6);
    if (dp > 0f)
    {
        c.Ellipse(cx, bot-12+oy, 8+(int)(dp*10), 8+(int)(dp*8), FO);
        c.Ellipse(cx, bot-12+oy, 4+(int)(dp*6), 4+(int)(dp*5), FY);
    }

    c.Outline(fx, fy, SZ, SZ, K);
}

// Behemoth — Cyberdemon hybrid (iron body, rocket arm, one cloven hoof)
static void DrawBehemoth(Canvas c, int fx, int fy, Anim a, int f)
{
    c.Clear();
    int cx = fx+16, bot = fy+30;
    int bob = Bob(a, f, 1);
    int oy = -bob;

    // single large horn
    c.Tri(cx, bot-32+oy, cx-4, bot-28+oy, cx+4, bot-28+oy, GR);
    // head — square, iron-plated
    c.Rect(cx-8, bot-28+oy, 17, 12, DG);
    c.Rect(cx-7, bot-27+oy, 15, 10, GR);
    // single red glowing eye
    c.Ellipse(cx-2, bot-24+oy, 3, 3, DR);
    c.Ellipse(cx-2, bot-24+oy, 2, 2, BR);
    // metal brow plates
    c.Rect(cx-7, bot-28+oy, 14, 3, DM);

    // massive torso — iron gray
    c.Ellipse(cx, bot-14+oy, 13, 11, DG);
    c.Ellipse(cx, bot-15+oy, 11, 10, GR);
    // chest armor plate
    c.Rect(cx-8, bot-20+oy, 17, 9, DM);
    c.Rect(cx-6, bot-18+oy, 13, 6, GR);
    c.Rect(cx-4, bot-17+oy, 8, 4, DG); // chest recess

    // right arm: ROCKET LAUNCHER
    if (a == Anim.Attack && f <= 1)
    {
        c.Rect(cx+7, bot-19+oy, 14, 6, DM); // launcher raised
        c.Rect(cx+7, bot-20+oy, 16, 5, MT);
        c.Rect(cx+20, bot-19+oy, 3, 4, DG); // barrel
        c.Set(cx+23, bot-18+oy, FO);          // muzzle flash
    }
    else
    {
        c.Rect(cx+7, bot-17+oy, 5, 8, DG);
        c.Rect(cx+10, bot-18+oy, 9, 5, DM); // launcher barrel horizontal
        c.Rect(cx+18, bot-17+oy, 3, 3, DG);
    }

    // left arm: organic, clawed
    int la = ArmSwing(a, f, true);
    c.Ellipse(cx-13, bot-16+oy+la, 4, 6, B);
    c.Ellipse(cx-13, bot-10+oy+la, 4, 5, T);
    c.Tri(cx-13, bot-5+oy+la, cx-17, bot-2+oy+la, cx-10, bot-2+oy+la, DF); // claw

    // mechanical right leg + organic left leg
    c.Rect(cx+3, bot-3+oy, 6, 8, DM);  // mechanical shin
    c.Rect(cx+2, bot+5+oy, 8, 3, MT);  // mechanical foot
    c.Rect(cx+4, bot-6+oy, 5, 4, DG);  // hip metal
    int ll = LegFwd(a, f, true);
    c.Rect(cx-8, bot-3+oy, 6, 7+ll, B); // organic leg
    c.Rect(cx-10, bot+4+oy, 8, 3, DB);  // organic hoof

    float dp = DeathProg(a, f, 6);
    if (dp > 0f)
    {
        c.Ellipse(cx, bot-10, (int)(dp*14), (int)(dp*12), FO);
        if (dp > 0.5f) c.Ellipse(cx, bot-10, (int)(dp*8), (int)(dp*7), FY);
    }

    c.Outline(fx, fy, SZ, SZ, K);
}

// Arachnocortex — Spider Mastermind (brain on mechanical legs, heavy gun)
static void DrawArachnocortex(Canvas c, int fx, int fy, Anim a, int f)
{
    c.Clear();
    int cx = fx+16, bot = fy+30;
    int bob = Bob(a, f, 1);
    int oy = -bob;

    // spider legs (3 each side)
    for (int i = 0; i < 3; i++)
    {
        int legY = bot - 8 - i*5 + oy;
        int spread = 7 + i*3;
        int legSwing = (a == Anim.Move) ? (i%2==0?LegFwd(a,f,true):LegFwd(a,f,false))*2 : 0;
        c.Rect(cx - spread - 4, legY + legSwing, 5, 2, DM);
        c.Rect(cx - spread - 2, legY + 2 + legSwing, 3, 3, DM);
        c.Rect(cx + spread,     legY + legSwing, 5, 2, DM);
        c.Rect(cx + spread + 2, legY + 2 + legSwing, 3, 3, DM);
    }

    // body housing — dark metal box
    c.Rect(cx-10, bot-20+oy, 21, 16, DM);
    c.Rect(cx-9, bot-19+oy, 19, 14, GR);

    // skull face on front
    c.Ellipse(cx, bot-15+oy, 6, 6, DG);
    c.Ellipse(cx-3, bot-17+oy, 2, 2, DG); // eye sockets
    c.Ellipse(cx+3, bot-17+oy, 2, 2, DG);
    c.Set(cx-3, bot-17+oy, FO); c.Set(cx+3, bot-17+oy, FO);

    // brain on top (pink, pulsing)
    Color brainC = (a == Anim.Special || (a == Anim.Attack && f%2==0)) ? LP : PK;
    c.Ellipse(cx, bot-25+oy, 10, 7, brainC);
    c.Ellipse(cx, bot-26+oy, 8, 6, LP);
    // brain folds
    for (int i = -3; i <= 3; i+=2) c.Set(cx+i, bot-25+oy, DP);

    // heavy gun barrel (center-front)
    if (a == Anim.Attack && f <= 2)
    {
        c.Rect(cx-3, bot-15+oy, 6, 4, DM);
        c.Rect(cx-4, bot-15+oy, 8, 3, MT);
        c.Rect(cx-2, bot-16+oy, 4, 2, MT); // gun recoil up
        c.Ellipse(cx, bot-10+oy, 3, 2, FO); // muzzle flash
    }
    else
    {
        c.Rect(cx-3, bot-12+oy, 7, 4, DM);
        c.Rect(cx-2, bot-13+oy, 5, 2, MT);
        c.Rect(cx+4,  bot-12+oy, 4, 3, DG); // barrel tip
    }

    float dp = DeathProg(a, f, 6);
    if (dp > 0f)
    {
        c.Ellipse(cx, bot-15, 11+(int)(dp*6), 9+(int)(dp*5), FO);
        c.Ellipse(cx, bot-15, (int)(7*dp), (int)(5*dp), FY);
    }

    c.Outline(fx, fy, SZ, SZ, K);
}

// Agaures — Arch-vile (tall pale fire-summoner)
static void DrawAgaures(Canvas c, int fx, int fy, Anim a, int f)
{
    c.Clear();
    int cx = fx+16, bot = fy+30;
    int bob = Bob(a, f, 2);
    int oy = -bob;

    // fire effect on hands (more intense on attack/special)
    bool fire = (a == Anim.Attack || a == Anim.Special);
    int fireInt = fire ? 5 + f : 2;
    int la = (a == Anim.Attack) ? -4 - f*2 : (a == Anim.Move ? ArmSwing(a,f,true)*2 : -8);
    int ra = (a == Anim.Attack) ? -4 - f*2 : (a == Anim.Move ? ArmSwing(a,f,false)*2 : -8);

    // tall thin body — arms always raised
    // left arm raised
    c.Rect(cx-11, bot-22+oy+la, 4, 13, BN);
    c.Ellipse(cx-9, bot-22+oy+la, 3, 3, BN);  // shoulder
    // fire at left hand
    c.Ellipse(cx-11, bot-22+oy+la, fireInt, fireInt, FO);
    if (fire) c.Ellipse(cx-11, bot-22+oy+la, fireInt-2, fireInt-2, FY);

    // right arm raised
    c.Rect(cx+7, bot-22+oy+ra, 4, 13, BN);
    c.Ellipse(cx+9, bot-22+oy+ra, 3, 3, BN);
    c.Ellipse(cx+11, bot-22+oy+ra, fireInt, fireInt, FO);
    if (fire) c.Ellipse(cx+11, bot-22+oy+ra, fireInt-2, fireInt-2, FY);

    // elongated head — pale and gaunt
    c.Ellipse(cx, bot-27+oy, 6, 7, BN);
    c.Ellipse(cx, bot-28+oy, 5, 6, LN);
    // deep dark eye sockets
    c.Ellipse(cx-3, bot-29+oy, 2, 3, DG);
    c.Ellipse(cx+3, bot-29+oy, 2, 3, DG);
    c.Set(cx-3, bot-29+oy, FO); c.Set(cx+3, bot-29+oy, FO); // glowing pupils
    // gaunt cheeks
    c.Set(cx-4, bot-26+oy, DB); c.Set(cx+4, bot-26+oy, DB);
    // teeth row
    for (int i = 0; i < 4; i++) c.Set(cx-3+i*2, bot-23+oy, WH);

    // very thin neck
    c.Rect(cx-2, bot-21+oy, 4, 4, BN);

    // torso — tall and thin
    c.Rect(cx-5, bot-17+oy, 11, 14, BN);
    c.Rect(cx-4, bot-16+oy, 9, 12, LN);
    // rib outlines
    for (int rib = 0; rib < 4; rib++)
    {
        int ry = bot-16+rib*3+oy;
        c.Set(cx-3, ry, DB); c.Set(cx+3, ry, DB);
    }
    // pale skin sheen on attack
    if (fire)
    {
        c.Ellipse(cx, bot-11+oy, 4+(f%2), 5+(f%2), Color.FromArgb(180, LN));
    }

    // legs — thin
    int ll = LegFwd(a, f, true);
    int rl = LegFwd(a, f, false);
    c.Rect(cx-5, bot-3+oy, 4, 6+ll, BN);
    c.Rect(cx+1, bot-3+oy, 4, 6-rl, BN);
    c.Rect(cx-6, bot+3+oy, 5, 2, B);
    c.Rect(cx+1, bot+3+oy, 5, 2, B);

    float dp = DeathProg(a, f, 6);
    if (dp > 0f)
    {
        c.Ellipse(cx, bot-14, (int)(dp*12), (int)(dp*16), FO);
        if (dp > 0.5f) { c.Rect(fx+4, fy+24, 24, 4, B); }
    }

    c.Outline(fx, fy, SZ, SZ, K);
}

// ── WEAPON SPRITES ────────────────────────────────────────────────────────────
// All weapons drawn from first-person perspective (bottom of 64×64 frame)

// AMP Pistol — classic military pistol (bottom-right of screen)
static void DrawAmpPistol(Canvas c, int fx, int fy, int frame)
{
    c.Clear();
    int bx = fx+22, by = fy+30; // hand pivot
    int kick = frame > 0 ? -(frame * 2) : 0; // recoil: kick up

    // hand / wrist (flesh)
    c.Ellipse(bx, by+2, 5, 4, DF);
    c.Rect(bx-4, by-2, 9, 5, FL);

    // grip (dark gray checkered)
    c.Rect(bx-3, by-10+kick, 7, 9, DG);
    c.Rect(bx-2, by-9+kick, 5, 7, GR);
    for (int i = 0; i < 3; i++) for (int j = 0; j < 2; j++)
        c.Set(bx-1+j*3, by-8+i*2+kick, DG); // grip texture

    // slide / frame
    c.Rect(bx-3, by-16+kick, 6, 7, GR);
    c.Rect(bx-2, by-15+kick, 4, 5, LG);
    c.Set(bx+2, by-14+kick, DG);  // ejection port

    // barrel
    c.Rect(bx-1, by-20+kick, 3, 5, DM);
    c.Rect(bx,   by-21+kick, 1, 2, DG); // muzzle

    // trigger guard
    c.Rect(bx+1, by-10+kick, 1, 4, GR);
    c.Set(bx+1, by-7+kick, DG);

    // muzzle flash on fire frames
    if (frame >= 2)
    {
        c.Ellipse(bx, by-22+kick, 3+(frame-1), 4+(frame-1), FY);
        c.Ellipse(bx, by-22+kick, 2, 3, WH);
    }

    c.Outline(fx, fy, SZ, SZ, K);
}

// Bear Killer — pump-action shotgun
static void DrawBearKiller(Canvas c, int fx, int fy, int frame)
{
    c.Clear();
    int kick = frame > 0 ? -(frame) : 0;

    // right hand on grip
    c.Rect(17, 26, 9, 5, FL);
    c.Rect(17, 29, 8, 3, DF);

    // stock (wood)
    c.Rect(16, 17+kick, 6, 12, WD);
    c.Rect(17, 18+kick, 4, 10, LW);
    // receiver (metal)
    c.Rect(10, 14+kick, 15, 7, DM);
    c.Rect(11, 15+kick, 13, 5, MT);
    c.Rect(11, 14+kick, 13, 2, DG); // top rail
    // pump handle (left hand + wood)
    c.Rect(6, 16+kick, 8, 4, WD);
    c.Rect(5, 23, 9, 4, FL);    // left hand
    // barrels (two tubes)
    c.Rect(5, 8+kick, 5, 7, DG);
    c.Rect(5, 7+kick, 6, 2, DM);
    c.Rect(6, 6+kick, 4, 2, DG); // barrel end
    // muzzle flash
    if (frame >= 2)
    {
        c.Ellipse(7, 7+kick, 4+frame, 5+frame, FY);
        c.Ellipse(7, 7+kick, 2, 3, WH);
    }

    c.Outline(fx, fy, SZ, SZ, K);
}

// H-Chaingun — multi-barrel minigun
static void DrawHChaingun(Canvas c, int fx, int fy, int frame)
{
    c.Clear();
    // spin the barrels (different offset per frame)
    int spin = frame * 2;
    int kick = frame > 0 ? -1 : 0;

    // left hand grip
    c.Rect(4, 24, 8, 5, FL); c.Rect(4, 27, 7, 3, DF);
    // right hand grip
    c.Rect(20, 24, 8, 5, FL); c.Rect(21, 27, 7, 3, DF);
    // ammo belt (right side)
    for (int i = 0; i < 6; i++) { c.Set(28+i%2, 22-i*2, KH); c.Set(29-i%2, 23-i*2, LK); }

    // main body housing
    c.Ellipse(16, 18+kick, 10, 8, DM);
    c.Ellipse(16, 17+kick, 9, 7, GR);
    c.Rect(7, 13+kick, 18, 7, DM);
    c.Rect(8, 14+kick, 16, 5, MT);

    // rotating barrels (4 barrels in a circle pattern)
    int[,] barrelOffsets = { {0,-4}, {4,0}, {0,4}, {-4,0} };
    for (int i = 0; i < 4; i++)
    {
        int bOff = (i + spin/2) % 4;
        int bx = barrelOffsets[bOff,0], bby = barrelOffsets[bOff,1];
        c.Rect(14+bx, 6+bby+kick, 4, 8, DG);
        c.Set(15+bx, 6+bby+kick, DM);
    }
    // barrel tips
    c.Rect(8, 5+kick, 15, 3, DG);
    c.Rect(9, 4+kick, 13, 2, DM);

    // muzzle flash (rotates with spin)
    if (frame >= 1)
    {
        c.Ellipse(16, 4+kick, 3+frame, 4+frame, FY);
        c.Ellipse(16, 4+kick, 2, 2, WH);
    }

    c.Outline(fx, fy, SZ, SZ, K);
}

// Auto Cannon — rocket launcher, large tube
static void DrawAutoCannon(Canvas c, int fx, int fy, int frame)
{
    c.Clear();
    int kick = frame > 0 ? -(frame*2) : 0;

    // right hand
    c.Rect(18, 25, 10, 5, FL); c.Rect(19, 28, 9, 3, DF);
    // left hand support
    c.Rect(5, 19, 10, 5, FL); c.Rect(6, 21, 8, 4, DF);

    // main tube (large)
    c.Rect(4, 8+kick, 24, 12, DM);
    c.Rect(5, 9+kick, 22, 10, MT);
    c.Rect(6, 10+kick, 20, 8, GR);
    // top sight
    c.Rect(12, 7+kick, 3, 3, DM);
    c.Rect(13, 6+kick, 1, 2, DM);
    // exhaust ports on tube
    for (int i = 0; i < 3; i++) c.Rect(8+i*5, 15+kick, 2, 3, DG);
    // muzzle opening
    c.Ellipse(4, 14+kick, 3, 5, DG);
    c.Ellipse(4, 14+kick, 2, 4, DB);

    // rocket visible in tube
    if (frame == 0)
    {
        c.Rect(5, 11+kick, 18, 6, R);
        c.Tri(5, 11+kick, 5, 17+kick, 2, 14+kick, BR); // warhead
    }

    // back-blast on fire
    if (frame >= 2)
    {
        c.Ellipse(4, 14+kick, 3+(frame*2), 5+(frame*2), FO);
        c.Ellipse(4, 14+kick, 2+frame, 3+frame, FY);
    }

    c.Outline(fx, fy, SZ, SZ, K);
}

// Dual Berettas — two pistols (energy/plasma variant)
static void DrawDualBerettas(Canvas c, int fx, int fy, int frame)
{
    c.Clear();
    int kick = frame > 0 ? -(frame) : 0;

    // left pistol
    c.Rect(3, 20+kick, 5, 7, DG); c.Rect(4, 21+kick, 3, 5, GR);
    c.Rect(2, 14+kick, 7, 7, DG); c.Rect(3, 15+kick, 5, 5, MT);
    c.Rect(3, 10+kick, 4, 5, DM);
    // energy coil on left
    c.Ellipse(5, 13+kick, 3, 3, Color.FromArgb(180,80,80,180));
    c.Set(5, 13+kick, Color.FromArgb(255,120,120,220));
    c.Rect(2, 24, 8, 5, DF); // left hand

    // right pistol (mirrored)
    c.Rect(24, 20+kick, 5, 7, DG); c.Rect(25, 21+kick, 3, 5, GR);
    c.Rect(23, 14+kick, 7, 7, DG); c.Rect(24, 15+kick, 5, 5, MT);
    c.Rect(25, 10+kick, 4, 5, DM);
    // energy coil on right
    c.Ellipse(27, 13+kick, 3, 3, Color.FromArgb(180,80,80,180));
    c.Set(27, 13+kick, Color.FromArgb(255,120,120,220));
    c.Rect(22, 24, 8, 5, DF); // right hand

    // plasma glow effect (center, on fire)
    if (frame >= 1)
    {
        c.Ellipse(5, 10+kick, 2+frame, 3+frame, Color.FromArgb(220,120,160,255));
        c.Ellipse(27, 10+kick, 2+frame, 3+frame, Color.FromArgb(220,120,160,255));
        c.Set(5, 9+kick, WH); c.Set(27, 9+kick, WH);
    }

    c.Outline(fx, fy, SZ, SZ, K);
}

// ── Sheet generation ─────────────────────────────────────────────────────────

delegate void DrawDelegate(Canvas c, int fx, int fy, Anim a, int frame);
delegate void DrawWeaponDelegate(Canvas c, int fx, int fy, int frame);

static void SaveEnemySprites(string dir, string name, DrawDelegate draw)
{
    Directory.CreateDirectory(dir);

    // Single frame (idle frame 0)
    var singleCanvas = new Canvas(SZ, SZ);
    draw(singleCanvas, 0, 0, Anim.Idle, 0);
    SaveUpscaled(singleCanvas, Path.Combine(dir, name + ".png"));

    // Full sheet: each frame drawn on its own fresh canvas, then 2× blitted into sheet
    var sheetDst = new Canvas(COLS * OUT_SZ, ROWS * OUT_SZ);
    Anim[] anims = { Anim.Idle, Anim.Move, Anim.Attack, Anim.Special, Anim.Death };
    for (int row = 0; row < ROWS; row++)
    {
        int frames = RowFrameCounts[row];
        for (int fr = 0; fr < frames; fr++)
        {
            var fc = new Canvas(SZ, SZ);
            draw(fc, 0, 0, anims[row], fr);
            BlitUpscaled(fc, sheetDst, fr * OUT_SZ, row * OUT_SZ);
        }
    }
    using var bmp = sheetDst.ToBitmap();
    bmp.Save(Path.Combine(dir, name + "_sheet.png"), ImageFormat.Png);
    Console.WriteLine($"  {name}");
}

static void SaveWeaponSprites(string dir, string name, DrawWeaponDelegate draw)
{
    Directory.CreateDirectory(dir);

    // Single idle sprite
    var single = new Canvas(SZ, SZ);
    draw(single, 0, 0, 0);
    SaveUpscaled(single, Path.Combine(dir, name + ".png"));

    // Fire sheet: 4 frames, each on a fresh canvas then 2× blitted
    var sheetDst = new Canvas(4 * OUT_SZ, OUT_SZ);
    for (int f = 0; f < 4; f++)
    {
        var fc = new Canvas(SZ, SZ);
        draw(fc, 0, 0, f);
        BlitUpscaled(fc, sheetDst, f * OUT_SZ, 0);
    }
    using var bmp = sheetDst.ToBitmap();
    bmp.Save(Path.Combine(dir, name + "_fire.png"), ImageFormat.Png);
    Console.WriteLine($"  {name}");
}

static void SaveUpscaled(Canvas src, string path)
{
    var dst = new Canvas(OUT_SZ, OUT_SZ);
    BlitUpscaled(src, dst, 0, 0);
    using var bmp = dst.ToBitmap();
    bmp.Save(path, ImageFormat.Png);
}

static void BlitUpscaled(Canvas src, Canvas dst, int dstX, int dstY)
{
    for (int y = 0; y < SZ; y++)
        for (int x = 0; x < SZ; x++)
        {
            Color c = src.Get(x, y);
            dst.Set(dstX + x*2,   dstY + y*2,   c);
            dst.Set(dstX + x*2+1, dstY + y*2,   c);
            dst.Set(dstX + x*2,   dstY + y*2+1, c);
            dst.Set(dstX + x*2+1, dstY + y*2+1, c);
        }
}

// ── Main ─────────────────────────────────────────────────────────────────────
public static int Run()
{

static string? FindImagesDir()
{
    string dir = AppContext.BaseDirectory;
    for (int i = 0; i < 10; i++)
    {
        string candidate = Path.Combine(dir, "Game", "Images");
        if (Directory.Exists(candidate)) return candidate;
        string parent = Path.GetDirectoryName(dir) ?? "";
        if (parent == dir) break;
        dir = parent;
    }
    return null;
}

string? imagesDir = FindImagesDir();
if (imagesDir == null)
{
    Console.WriteLine("ERROR: Could not find Game/Images directory. Run from the solution root.");
    return 1;
}

Console.WriteLine($"Output: {imagesDir}");

// Enemies
string enemyRoot = Path.Combine(imagesDir, "Enemy");
Console.WriteLine("Enemies:");
SaveEnemySprites(Path.Combine(enemyRoot, "zombie_scientist"), "zombie_scientist",
    (c, fx, fy, a, f) => DrawZombie(c, fx, fy, a, f));
SaveEnemySprites(Path.Combine(enemyRoot, "blind_pinky"), "blind_pinky",
    (c, fx, fy, a, f) => DrawPinky(c, fx, fy, a, f));
SaveEnemySprites(Path.Combine(enemyRoot, "slime_imp"), "slime_imp",
    (c, fx, fy, a, f) => DrawImp(c, fx, fy, a, f));
SaveEnemySprites(Path.Combine(enemyRoot, "blood_ghost"), "blood_ghost",
    (c, fx, fy, a, f) => DrawGhost(c, fx, fy, a, f));
SaveEnemySprites(Path.Combine(enemyRoot, "beam_revenant"), "beam_revenant",
    (c, fx, fy, a, f) => DrawRevenant(c, fx, fy, a, f));
SaveEnemySprites(Path.Combine(enemyRoot, "hellion"), "hellion",
    (c, fx, fy, a, f) => DrawHellion(c, fx, fy, a, f));

// Bosses
string bossRoot = Path.Combine(imagesDir, "Bosses");
Console.WriteLine("Bosses:");
SaveEnemySprites(Path.Combine(bossRoot, "azazel"), "azazel",
    (c, fx, fy, a, f) => DrawAzazel(c, fx, fy, a, f));
SaveEnemySprites(Path.Combine(bossRoot, "behemoth"), "behemoth",
    (c, fx, fy, a, f) => DrawBehemoth(c, fx, fy, a, f));
SaveEnemySprites(Path.Combine(bossRoot, "arachnocortex"), "arachnocortex",
    (c, fx, fy, a, f) => DrawArachnocortex(c, fx, fy, a, f));
SaveEnemySprites(Path.Combine(bossRoot, "agaures"), "agaures",
    (c, fx, fy, a, f) => DrawAgaures(c, fx, fy, a, f));

// Weapons
string gunRoot = Path.Combine(imagesDir, "Gun");
Console.WriteLine("Weapons:");
SaveWeaponSprites(Path.Combine(gunRoot, "amp_pistol"), "amp_pistol",
    (c, fx, fy, f) => DrawAmpPistol(c, fx, fy, f));
SaveWeaponSprites(Path.Combine(gunRoot, "bear_killer"), "bear_killer",
    (c, fx, fy, f) => DrawBearKiller(c, fx, fy, f));
SaveWeaponSprites(Path.Combine(gunRoot, "h_chaingun"), "h_chaingun",
    (c, fx, fy, f) => DrawHChaingun(c, fx, fy, f));
SaveWeaponSprites(Path.Combine(gunRoot, "auto_cannon"), "auto_cannon",
    (c, fx, fy, f) => DrawAutoCannon(c, fx, fy, f));
SaveWeaponSprites(Path.Combine(gunRoot, "dual_berettas"), "dual_berettas",
    (c, fx, fy, f) => DrawDualBerettas(c, fx, fy, f));

Console.WriteLine("Done.");
return 0;
} // Run()
} // class Gen
