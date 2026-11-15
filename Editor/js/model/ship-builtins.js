// The five ships that come with the game: outline (ship-local px, rounded to 0.5), colour, radius and mass, copied from Content/Ships.
// Used as ghost outlines to compare size, and as starting points to remix. Never written back to the game.
export const SHIPPED_SHIPS = {
  "Bat-ter Up": {"color":"FFFF0000","radius":15,"mass":20,"points":[[1,5],[0,3],[-1,5],[-3,1],[-8,4],[-9,8],[-10,4],[-10,-4],[-9,-8],[-8,-4],[-3,-1],[-1,-1],[0,-5],[1,-1],[3,-1],[8,-4],[9,-8],[10,-4],[10,4],[9,8],[8,4],[3,1]]},
  "Blaster": {"color":"FF00FF00","radius":20,"mass":32,"points":[[-2,-2],[-13,0],[-15,-10],[-15,2],[0,15],[15,2],[15,-10],[13,0],[2,-2],[2,-10],[0,-15],[-2,-10]]},
  "Defender": {"color":"FF6495ED","radius":20,"mass":32,"points":[[-15,-2],[-11,-6],[-7,-2],[0,-9],[7,-2],[11,-6],[15,-2],[4,9],[0,5],[-4,9]]},
  "Destroyer": {"color":"FFFF00FF","radius":20,"mass":32,"points":[[0,-16],[6,0],[12,-6],[12,2],[11,6],[8,10],[4,12],[0,13],[-4,12],[-8,10],[-11,6],[-12,2],[-12,-6],[-6,0]]},
  "Nimble": {"color":"FFFF0000","radius":15,"mass":20,"points":[[0,-10],[-5,0],[-2,10],[0,4],[2,10],[5,0]]}};
