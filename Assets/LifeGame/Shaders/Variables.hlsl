int _Width;
int _Height;

inline int XyToIdx(int2 xy)
{
    return xy.y * _Width  + xy.x;
}

