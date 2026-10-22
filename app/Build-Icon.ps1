param([string]$OutputDirectory = $PSScriptRoot)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore,WindowsBase
function Brush([string]$color) { return [Windows.Media.BrushConverter]::new().ConvertFromString($color) }
function ArcGeometry([double]$radius,[double]$from,[double]$to) {
    $a = $from * [Math]::PI / 180; $b = $to * [Math]::PI / 180
    $figure = [Windows.Media.PathFigure]::new()
    $figure.StartPoint = [Windows.Point]::new(128 + $radius * [Math]::Cos($a),128 + $radius * [Math]::Sin($a))
    $segment = [Windows.Media.ArcSegment]::new()
    $segment.Point = [Windows.Point]::new(128 + $radius * [Math]::Cos($b),128 + $radius * [Math]::Sin($b))
    $segment.Size = [Windows.Size]::new($radius,$radius)
    $segment.IsLargeArc = ($to - $from -gt 180)
    $segment.SweepDirection = [Windows.Media.SweepDirection]::Clockwise
    $figure.Segments.Add($segment)
    $geometry = [Windows.Media.PathGeometry]::new(); $geometry.Figures.Add($figure)
    return $geometry
}
function RenderIcon([int]$size) {
    $visual = [Windows.Media.DrawingVisual]::new(); $dc = $visual.RenderOpen()
    $dc.PushTransform([Windows.Media.ScaleTransform]::new($size / 256.0,$size / 256.0))
    $dc.DrawEllipse((Brush '#031319'),$null,[Windows.Point]::new(128,128),124,124)
    $dc.DrawEllipse($null,[Windows.Media.Pen]::new((Brush '#075A66'),3),[Windows.Point]::new(128,128),119,119)
    for ($i = 0; $i -lt 48; $i++) {
        $angle = $i * [Math]::PI / 24; $inner = if ($i % 4 -eq 0) { 107 } else { 111 }
        $dc.DrawLine([Windows.Media.Pen]::new((Brush '#00A5B5'),1.5),[Windows.Point]::new(128+$inner*[Math]::Cos($angle),128+$inner*[Math]::Sin($angle)),[Windows.Point]::new(128+115*[Math]::Cos($angle),128+115*[Math]::Sin($angle)))
    }
    $dc.DrawGeometry($null,[Windows.Media.Pen]::new((Brush '#00E5EC'),12),(ArcGeometry 94 -82 141))
    $dc.DrawGeometry($null,[Windows.Media.Pen]::new((Brush '#087180'),9),(ArcGeometry 94 152 244))
    $dc.DrawGeometry($null,[Windows.Media.Pen]::new((Brush '#F04040'),2),(ArcGeometry 103 201 271))
    $dc.DrawEllipse($null,[Windows.Media.Pen]::new((Brush '#0A8494'),3),[Windows.Point]::new(128,128),77,77)
    $dc.DrawGeometry($null,[Windows.Media.Pen]::new((Brush '#00BBCE'),4),(ArcGeometry 68 18 132))
    $dc.DrawGeometry($null,[Windows.Media.Pen]::new((Brush '#067282'),4),(ArcGeometry 68 160 291))
    $glow = [Windows.Media.RadialGradientBrush]::new([Windows.Media.ColorConverter]::ConvertFromString('#13D8EF'),[Windows.Media.ColorConverter]::ConvertFromString('#073F55'))
    $dc.DrawEllipse($glow,[Windows.Media.Pen]::new((Brush '#07C5DE'),2),[Windows.Point]::new(128,128),52,52)
    for ($i = 0; $i -lt 6; $i++) { $dc.DrawGeometry($null,[Windows.Media.Pen]::new((Brush '#0799BC'),7),(ArcGeometry 41 ($i*60+7) ($i*60+43))) }
    $orb = [Windows.Media.RadialGradientBrush]::new([Windows.Media.ColorConverter]::ConvertFromString('#69F4FF'),[Windows.Media.ColorConverter]::ConvertFromString('#0087BB'))
    $orb.GradientOrigin = [Windows.Point]::new(0.35,0.25)
    $dc.DrawEllipse($orb,[Windows.Media.Pen]::new((Brush '#32E9FF'),1.5),[Windows.Point]::new(128,128),24,24)
    $dc.Pop(); $dc.Close()
    $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new($size,$size,96,96,[Windows.Media.PixelFormats]::Pbgra32); $bitmap.Render($visual)
    $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new(); $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $buffer = [IO.MemoryStream]::new(); $encoder.Save($buffer); $bytes = $buffer.ToArray(); $buffer.Dispose()
    return ,$bytes
}
$sizes = @(16,24,32,48,64,128,256)
$images = @(); foreach ($size in $sizes) { $images += ,(RenderIcon $size) }
$stream = [IO.File]::Create((Join-Path $OutputDirectory 'Syra.ico')); $writer = [IO.BinaryWriter]::new($stream)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $dimension = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension); $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$images[$i].Length); $writer.Write([uint32]$offset)
        $offset += $images[$i].Length
    }
    foreach ($bytes in $images) { $writer.Write([byte[]]$bytes) }
} finally { $writer.Dispose(); $stream.Dispose() }
[IO.File]::WriteAllBytes((Join-Path $OutputDirectory 'Syra-icon.png'),[byte[]]$images[-1])
Write-Output 'Created multi-resolution arc-reactor icon (16-256 pixels).'
