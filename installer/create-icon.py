from pathlib import Path
from PIL import Image, ImageDraw

# Original geometric thread/spool mark, no game art or third-party logo.
root = Path(__file__).resolve().parent
dest = root / 'assets'
dest.mkdir(exist_ok=True)
im = Image.new('RGBA', (256, 256), (0, 0, 0, 0))
d = ImageDraw.Draw(im)
d.rounded_rectangle((0, 0, 255, 255), radius=52, fill='#1a2633')
d.ellipse((59, 27, 197, 229), outline='#e8e0ca', width=13)
for y in [79, 112, 145, 178]:
    d.arc((47, y-28, 209, y+34), 8, 164, fill='#e8e0ca', width=10)
d.arc((10, 67, 241, 251), 13, 127, fill='#52b6b8', width=13)
im.save(dest / 'setup.ico', sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)])
im.save(dest / 'setup-icon.png')
print('Original installer icon created.')
