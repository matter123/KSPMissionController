using System;
using System.Collections.Generic;
using UnityEngine;

namespace MissionController
{
    /// <summary>
    /// Manages the missions and the current space program (budget and so on)
    /// </summary>
    public class Manager : IManager
    {
        private Parser parser;

        private static Manager manager = new Manager();

        public static Manager instance {get { return manager; } }

        private SpaceProgram spaceProgram;
        private String currentTitle;

        public Manager() {
            currentTitle = "default (Sandbox)";
            parser = new Parser();
            loadProgram (currentTitle);
        }

        /// <summary>
        /// Reset the current space program.
        /// </summary>
        public void resetSpaceProgram() {
            spaceProgram = SpaceProgram.generate ();
        }

        /// <summary>
        /// Recycles the vessel with the given costs.
        /// It is added to the recycled vessels list.
        /// </summary>
        /// <param name="vessel">Vessel.</param>
        /// <param name="costs">Costs.</param>
        public void recycleVessel(Vessel vessel, int costs) {
            if (!isRecycledVessel (vessel)) {
                reward (costs);
                RecycledVessel rv = new RecycledVessel ();
                rv.guid = vessel.id.ToString ();
                currentProgram.add (rv);
            }
        }

        public void loadProgram(String title) {
            currentTitle = title;
            try {
                spaceProgram = (SpaceProgram) parser.readFile (currentSpaceProgramFile);
            } catch {
                spaceProgram = SpaceProgram.generate();
            }
        }

        /// <summary>
        /// Discards the given random mission.
        /// Removed it from the random missions list
        /// </summary>
        /// <param name="m">M.</param>
        public void discardRandomMission(Mission m) {
            if (m.randomized) {
                RandomMission rm = currentProgram.findRandomMission (m);
                if(rm != null) {
                    currentProgram.randomMissions.Remove (rm);
                }
            }
        }

        /// <summary>
        /// Loads the given mission package
        /// </summary>
        /// <returns>The mission package.</returns>
        /// <param name="path">Path.</param>
        public MissionPackage loadMissionPackage(String path) {
            MissionPackage pkg = (MissionPackage) parser.readFile (path);
            Mission.Sort (pkg.Missions, pkg.ownOrder ? SortBy.PACKAGE_ORDER : SortBy.NAME);
            return pkg;
        }

        /// <summary>
        /// Reloads the given mission for the given vessel. Checks for already finished mission goals
        /// and reexecutes the instructions.
        /// </summary>
        /// <returns>reloaded mission</returns>
        /// <param name="m">mission</param>
        /// <param name="vessel">vessel</param>
        public Mission reloadMission(Mission m, Vessel vessel) {
            int count = 1;

            if (m.randomized) {
                RandomMission rm = currentProgram.findRandomMission (m);
                System.Random random = null;

                if (rm == null) {
                    rm = new RandomMission ();
                    rm.seed = new System.Random ().Next ();
                    rm.missionName = m.name;
                    currentProgram.add (rm);
                }

                random = new System.Random (rm.seed);
                m.executeInstructions (random);
            } else {
                // Maybe there are some instructions. We have to execute them!!!
                m.executeInstructions (new System.Random());
            }

            foreach(MissionGoal c in m.goals) {
                c.id = m.name + "__PART" + (count++);
                c.repeatable = m.repeatable;
                c.doneOnce = false;
            }

            if (vessel != null) {
                foreach (MissionGoal g in m.goals) {
                    if(isMissionGoalAlreadyFinished(g, vessel) && g.nonPermanent) {
                        g.doneOnce = true;
                    }
                }
            }

            return m;
        }

        /// <summary>
        /// Returns the current space program.
        /// </summary>
        /// <value>The current program.</value>
        private SpaceProgram currentProgram {
            get { 
                if(spaceProgram == null) {
                    loadProgram(currentTitle);
                }
                return spaceProgram;
            }
        }

        /// <summary>
        /// Saves the current space program
        /// </summary>
        public void saveProgram() {
            if (spaceProgram != null) {
                parser.writeObject (spaceProgram, currentSpaceProgramFile);
            }
        }

        /// <summary>
        /// Returns the current file, in which the current space program has been saved
        /// </summary>
        /// <value>The current space program file.</value>
        private String currentSpaceProgramFile {
            get {
                return currentTitle + ".sp";
            }
        }

        /// <summary>
        /// Finishes the given mission goal with the given vessel.
        /// Rewards the space program with the reward from mission goal.
        /// </summary>
        /// <param name="goal">Goal.</param>
        /// <param name="vessel">Vessel.</param>
        public void finishMissionGoal(MissionGoal goal, Vessel vessel, GameEvent events) {
            if (!isMissionGoalAlreadyFinished (goal, vessel) && goal.nonPermanent && goal.isDone(vessel, events) &&
                    !isRecycledVessel(vessel)) {
                currentProgram.add(new GoalStatus(vessel.id.ToString(), goal.id));
                reward (goal.reward);
            }
        }

        /// <summary>
        /// Returns true, if the given mission goal has been finish in another game with the given vessel, false otherwise
        /// </summary>
        /// <returns><c>true</c>, if mission goal already finished, <c>false</c> otherwise.</returns>
        /// <param name="c">goal</param>
        /// <param name="v">vessel</param>
        public bool isMissionGoalAlreadyFinished(MissionGoal c, Vessel v) {
            if (v == null) {
                return false;
            }

            foreach (GoalStatus con in currentProgram.completedGoals) {
                // If the mission goal is an EVAGoal, we don't care about the vessel id. Otherwise we do...
                if(con.id.Equals(c.id) && (con.vesselGuid.Equals (v.id.ToString()) || c.vesselIndenpendent)) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Finishes the given mission with the given vessel.
        /// Rewards the space program with the missions reward.
        /// </summary>
        /// <param name="m">mission</param>
        /// <param name="vessel">vessel</param>
        public void finishMission(Mission m, Vessel vessel, GameEvent events) {
            if (!isMissionAlreadyFinished (m, vessel) && !isRecycledVessel(vessel) && m.isDone(vessel, events)) {
                MissionStatus status = new MissionStatus (m.name, vessel.id.ToString ());

                if (m.passiveMission) {
                    status.endOfLife = Planetarium.GetUniversalTime () + m.lifetime;
                    status.passiveReward = m.passiveReward;
                    status.lastPassiveRewardTime = Planetarium.GetUniversalTime ();
                }

                if(m.clientControlled) {
                    status.endOfLife = Planetarium.GetUniversalTime () + m.lifetime;
                }

                status.clientControlled = m.clientControlled;

                currentProgram.add(status);
                reward (m.reward);
                
                // finish unfinished goals
                foreach(MissionGoal goal in m.goals) {
                    finishMissionGoal(goal, vessel, events);
                }

                // If this mission is randomized, we will discard the mission
                if (m.randomized) {
                    discardRandomMission (m);
                    m = reloadMission (m, vessel);
                }
            }
        }

        /// <summary>
        /// If true, the given mission name has been finished. False otherwise.
        /// </summary>
        /// <returns><c>true</c>, if mission already finished, <c>false</c> otherwise.</returns>
        /// <param name="name">mission name</param>
        public bool isMissionAlreadyFinished(String name) {
            foreach (MissionStatus s in currentProgram.completedMissions) {
                if (s.missionName.Equals (name)) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Returns true, if the given mission has been finished with the given vessel.
        /// </summary>
        /// <returns><c>true</c>, if mission already finished, <c>false</c> otherwise.</returns>
        /// <param name="m">mission</param>
        /// <param name="v">vessel</param>
        public bool isMissionAlreadyFinished(Mission m, Vessel v) {
            if (v == null) {
                return false;
            }

            foreach (MissionStatus s in currentProgram.completedMissions) {
                if(s.missionName.Equals(m.name)) {
                    if(m.repeatable) {
                        if(s.vesselGuid.Equals(v.id.ToString())) {
                            return true;
                        }
                    } else {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Gets all passive missions that are currently active. Removes old passive missions
        /// </summary>
        /// <returns>The passive missions.</returns>
        public List<MissionStatus> getActivePassiveMissions() {
            List<MissionStatus> status = new List<MissionStatus> ();
            List<MissionStatus> removable = new List<MissionStatus> ();
            foreach (MissionStatus s in currentProgram.completedMissions) {
                if(s.endOfLife != 0.0) {
                    if (s.endOfLife <= Planetarium.GetUniversalTime ()) {
                        removable.Add (s);
                    } else {
                        status.Add (s);
                    }
                }
            }

            // Cleanup old missions
            // We don't clean up for now. We want to show how long the mission is still active
            // and if it not active any longer, we will show it.
            foreach (MissionStatus r in removable) {
                currentProgram.completedMissions.Remove (r);
            }
            return status;
        }

        public List<MissionStatus> getClientControlledMissions() {
            List<MissionStatus> status = new List<MissionStatus> ();
            List<MissionStatus> removable = new List<MissionStatus> ();
            foreach (MissionStatus s in currentProgram.completedMissions) {
                if(s.clientControlled) {
                    if (s.endOfLife <= Planetarium.GetUniversalTime ()) {
                        removable.Add (s);
                    } else {
                        status.Add (s);
                    }
                }
            }

            // Cleanup old missions
            // We don't clean up for now. We want to show how long the mission is still active
            // and if it not active any longer, we will show it.
            foreach (MissionStatus r in removable) {
                currentProgram.completedMissions.Remove (r);
            }

            return status;
        }

        /// <summary>
        /// Gets the client controlled mission that has been finished with the given vessel
        /// </summary>
        /// <returns>The client controlled mission.</returns>
        /// <param name="vessel">Vessel.</param>
        public MissionStatus getClientControlledMission(Vessel vessel) {
            foreach (MissionStatus s in currentProgram.completedMissions) {
                if(s.clientControlled && s.vesselGuid.Equals(vessel.id.ToString())) {
                    return s;
                }
            }
            return null;
        }

        /// <summary>
        /// Returns the currently available budget.
        /// </summary>
        /// <value>The budget.</value>
        public int budget {
            get { return currentProgram.money; }
        }

        /// <summary>
        /// Checks if the given vessel has been recycled previously. 
        /// </summary>
        /// <returns><c>true</c>, if vessel has been recycled, <c>false</c> otherwise.</returns>
        /// <param name="vessel">vessel</param>
        public bool isRecycledVessel(Vessel vessel) {
            if (vessel == null) {
                return false;
            }

            foreach (RecycledVessel rv in currentProgram.recycledVessels) {
                if (rv.guid.Equals (vessel.id.ToString())) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Checks if this vessel is controlled by a client.
        /// </summary>
        /// <returns><c>true</c>, if vessel is controlled by a client, <c>false</c> otherwise.</returns>
        /// <param name="vessel">Vessel.</param>
        public bool isClientControlled(Vessel vessel) {
            foreach (MissionStatus status in currentProgram.completedMissions) {
                if (status.clientControlled && status.vesselGuid.Equals (vessel.id.ToString()) &&
                        status.endOfLife >= Planetarium.GetUniversalTime()) {
                    return true;
                }
            }
            return false;
        }

        public void removeMission(MissionStatus s) {
            currentProgram.completedMissions.Remove (s);
        }

        // The interface implementation for external plugins

        public int getBudget() {
            return currentProgram.money;
        }

        public int reward(int value) {
            if (!SettingsManager.Manager.getSettings ().DisablePlugin) {
                currentProgram.money += value;
            }
            return currentProgram.money;
        }

        public int costs(int value) {
            return reward (-value);
        }
    }
}

